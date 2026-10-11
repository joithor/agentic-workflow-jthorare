using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Blocks;

namespace Aw.Sindri.Core.Workflow;

/// <summary>Applies a signal: version check (an exact retry is a no-op), candidates, gates, then the transition or the park or escalation in its place.</summary>
/// <param name="store">The workflow store (for the history an exact retry is found in).</param>
/// <param name="gates">The gate evaluator.</param>
/// <param name="builder">The commit builder.</param>
/// <param name="followUps">Effects and child starts.</param>
/// <param name="blocks">The block registry.</param>
/// <param name="clock">The clock.</param>
internal sealed class SignalApplier(IWorkflowStore store, GateEvaluator gates, CommitBuilder builder, FollowUps followUps, IBlockRegistry blocks, TimeProvider clock)
{
    internal async ValueTask<Result<(CommitDraft Draft, AppliedChange Trigger, IReadOnlyList<GateRecord> Gates)?>> Decide(LoadedInstance loaded, WorkflowSignal signal,
        CancellationToken ct)
    {
        var (instance, recipe) = (loaded.Instance, loaded.Recipe.Recipe);
        if (signal.ExpectedVersion is { } expected && expected != instance.Version)
        {
            return await Retried(instance, signal, expected, ct).ConfigureAwait(false)
                ? Results.Success<(CommitDraft, AppliedChange, IReadOnlyList<GateRecord>)?>(value: null)
                : SindriErrors.InstanceChanged.With(string.Create(CultureInfo.InvariantCulture, $"{instance.Id.Value} is at version {instance.Version}, not {expected} (fix: sindri workflow show {instance.Id.Value})"));
        }

        var candidates = SignalDecisions.Candidates(recipe, instance, signal, blocks);
        return candidates.IsError ? candidates.Error : await First(loaded, signal, candidates.Value, ct).ConfigureAwait(false);
    }

    // The first candidate whose gates pass (or that escalates); otherwise the last refusal.
    private async ValueTask<Result<(CommitDraft Draft, AppliedChange Trigger, IReadOnlyList<GateRecord> Gates)?>> First(LoadedInstance loaded, WorkflowSignal signal,
        IReadOnlyList<Recipes.RecipeTransition> candidates, CancellationToken ct)
    {
        var (instance, recipe) = (loaded.Instance, loaded.Recipe.Recipe);
        Error? refused = null;
        foreach (var transition in candidates)
        {
            var records = await gates.Evaluate(instance, transition, signal, ct).ConfigureAwait(false);
            if (records.IsError)
            {
                return records.Error;
            }

            var now = clock.GetUtcNow();
            var judged = GateOutcome.Judge(instance, records.Value, now)
                ?? SignalDecisions.Settle(new TransitionInput(instance, recipe, transition, signal, records.Value, now));
            if (judged.IsError)
            {
                refused = judged.Error;
                continue;
            }

            var drafted = await Draft(loaded, transition, signal, judged.Value, ct).ConfigureAwait(false);
            return drafted.IsError ? drafted.Error : (drafted.Value.Draft, drafted.Value.Trigger, records.Value);
        }

        return refused ?? SindriErrors.TransitionNotAllowed.With($"no transition from '{instance.State}' on '{signal.On}'");
    }

    private async ValueTask<Result<(CommitDraft Draft, AppliedChange Trigger)>> Draft(LoadedInstance loaded, Recipes.RecipeTransition transition, WorkflowSignal signal,
        WorkflowChange change, CancellationToken ct)
    {
        var draft = new CommitDraft();
        var instance = loaded.Instance;
        var effects = (transition.Effects ?? []).Select(id => blocks.Find(id)?.CommandKind).OfType<string>().ToList();
        var recorded = change.Kind is ChangeKind.Transitioned ? change with { Effects = effects } : change;
        var applied = builder.Record(draft, instance, recorded, WorkflowKeys.Transition(instance, transition.To, transition.On, signal.Sha));
        if (recorded.Kind is ChangeKind.Transitioned)
        {
            var follow = await followUps.After(draft, loaded.Recipe.Recipe, transition, applied, ct).ConfigureAwait(false);
            if (follow.IsError)
            {
                return follow.Error;
            }
        }

        return (draft, new AppliedChange(applied.After, recorded, applied.Event));
    }

    // An exact retry: the signal already recorded a change at the version the caller expected.
    private async ValueTask<bool> Retried(WorkflowInstance instance, WorkflowSignal signal, long expected, CancellationToken ct)
    {
        if (expected > instance.Version)
        {
            return false;
        }

        var history = await store.History(instance.Id, ct).ConfigureAwait(false);
        var suffix = WorkflowKeys.SignalSuffix(signal.On, expected, signal.Sha);
        return history.IsSuccess && history.Value.Any(e => e.IdempotencyKey.EndsWith(suffix, StringComparison.Ordinal));
    }
}
