using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>The pure decisions for a signal: which transitions it may take, and what applying the chosen one does (evidence, budget, loops, no-progress).</summary>
public static class SignalDecisions
{
    /// <summary>The transitions <paramref name="signal"/> may take from the instance's state, in recipe order.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <param name="instance">The instance.</param>
    /// <param name="signal">The signal.</param>
    /// <param name="blocks">The block registry (for the state's step outputs).</param>
    /// <returns>The candidates, or SND-WF-005 (not running, no such transition) or SND-WF-009 (an output the step doesn't declare).</returns>
    public static Result<IReadOnlyList<RecipeTransition>> Candidates(Recipe recipe, WorkflowInstance instance, WorkflowSignal signal, IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(blocks);
        if (!instance.Runs)
        {
            return SindriErrors.TransitionNotAllowed.With($"{instance.Id.Value} is {WorkflowWords.Status(instance)}; it takes no signals");
        }

        var outputs = OutputProblem(recipe, instance, signal, blocks);
        if (outputs is not null)
        {
            return outputs;
        }

        var candidates = recipe.Transitions.Where(t => t.From == instance.State && t.On == signal.On).ToList();
        if (candidates.Count > 0)
        {
            return candidates;
        }

        var allowed = recipe.Transitions.Where(t => t.From == instance.State).Select(t => t.On).Distinct(StringComparer.Ordinal).ToList();
        return SindriErrors.TransitionNotAllowed.With(
            $"no transition from '{instance.State}' on '{signal.On}' (allowed: {(allowed.Count == 0 ? "none" : string.Join(", ", allowed))})");
    }

    /// <summary>
    /// What applying <paramref name="input"/>'s transition does once its gates passed: the transition, or a park
    /// (budget, rounds, no-progress) in its place. Exhaustion is a transition to parked, not an error (§17.28.1).
    /// </summary>
    /// <param name="input">The instance, recipe, transition, signal and gate records.</param>
    /// <returns>The change, or SND-WF-007 when required evidence isn't registered.</returns>
    public static Result<WorkflowChange> Settle(TransitionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Missing(input.Instance, input.Transition, input.Signal) is { } missing ? missing : Budget(input) ?? Loop(input) ?? Transitioned(input);
    }

    /// <summary>
    /// The evidence check, made before any gate runs (§17.28.1: evidence first, then the gate ladder): SND-WF-007 when
    /// <paramref name="transition"/> needs evidence that isn't registered for the instance and the signal's sha.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="transition">The candidate transition.</param>
    /// <param name="signal">The signal (its sha binds 'kind@head').</param>
    /// <returns>Null when every requirement is registered; otherwise the refusal.</returns>
    public static Error? Missing(WorkflowInstance instance, RecipeTransition transition, WorkflowSignal signal)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(signal);
        var missing = (transition.Evidence ?? []).Where(requirement => !Registered(instance, requirement, signal.Sha)).ToList();
        return missing.Count == 0 ? null : SindriErrors.EvidenceMissing.With($"'{transition.From}' -> '{transition.To}' needs {string.Join(", ", missing)} registered for this instance");
    }

    private static Error? OutputProblem(Recipe recipe, WorkflowInstance instance, WorkflowSignal signal, IBlockRegistry blocks)
    {
        var declared = recipe.State(instance.State)?.Step is { } step && blocks.Find(step) is { } spec ? spec.Outputs : [];
        foreach (var (name, value) in signal.Outputs)
        {
            var port = declared.FirstOrDefault(p => p.Name == name);
            if (port is null || !port.Accepts(value))
            {
                return SindriErrors.OutputInvalid.With(port is null
                    ? $"'{name}' isn't an output of the step in '{instance.State}' (outputs: {(declared.Count == 0 ? "none" : string.Join(", ", declared))})"
                    : $"'{value}' isn't a valid {port}");
            }
        }

        return null;
    }

    private static bool Registered(WorkflowInstance instance, string requirement, Ids.HeadSha? head)
    {
        var (kind, binding) = RecipeBlockChecks.Requirement(requirement) ?? (requirement, null);
        var bound = binding switch
        {
            "head" => head,
            "base" => instance.BaseSha,
            _ => null,
        };
        return instance.Evidence.Any(e => e.Kind == kind && (binding is null || e.Sha == bound));
    }

    private static WorkflowChange? Budget(TransitionInput input)
    {
        var (instance, recipe, signal) = (input.Instance, input.Recipe, input.Signal);
        var spent = instance.Spent.Plus(signal.Spend);
        var inState = instance.StateSpent.Plus(signal.Spend);
        var over = Over(spent, recipe.Budget, instance.Extra) ?? Over(inState, recipe.State(instance.State)?.Budget, Spend.Zero);
        return over is null ? null : Park(input, new InstanceHold(HoldKind.Parked, ParkReason.Budget, over));
    }

    private static string? Over(Spend spent, BudgetSpec? budget, Spend extra) => budget switch
    {
        { Tokens: { } tokens } when spent.Tokens > tokens + extra.Tokens => string.Create(CultureInfo.InvariantCulture, $"{spent.Tokens} tokens spent of {tokens + extra.Tokens}"),
        { Turns: { } turns } when spent.Turns > turns + extra.Turns => string.Create(CultureInfo.InvariantCulture, $"{spent.Turns} turns spent of {turns + extra.Turns}"),
        _ => null,
    };

    private static WorkflowChange? Loop(TransitionInput input)
    {
        if (input.Transition.Loop is not { } loop)
        {
            return null;
        }

        var round = input.Instance.Loops.GetValueOrDefault(loop) + 1;
        var cap = input.Recipe.Loops?.GetValueOrDefault(loop)?.Cap ?? 0;
        if (round > cap)
        {
            return Park(input, new InstanceHold(HoldKind.Parked, ParkReason.Rounds, string.Create(CultureInfo.InvariantCulture, $"{loop}: round {round} is past the cap of {cap}"), loop));
        }

        var previous = input.Instance.Findings.GetValueOrDefault(loop);
        return NoProgress.Detected(previous, input.Signal.Findings, input.Signal.EmptyDiff)
            ? Park(input, new InstanceHold(HoldKind.Parked, ParkReason.NoProgress, NoProgressDetail(loop, input.Signal), loop))
            : null;
    }

    private static string NoProgressDetail(string loop, WorkflowSignal signal) => signal.EmptyDiff
        ? $"{loop}: the round produced an empty diff"
        : $"{loop}: the same findings again: {string.Join("; ", signal.Findings)}";

    private static WorkflowChange Park(TransitionInput input, InstanceHold hold) =>
        new(input.Instance.Id, ChangeKind.Parked, input.Instance.Version + 1, input.At, Sha: input.Signal.Sha, Gates: input.Gates,
            Loop: hold.Loop, Findings: hold.Loop is null ? null : input.Signal.Findings, Spend: input.Signal.Spend, Hold: hold, Detail: hold.Detail);

    private static WorkflowChange Transitioned(TransitionInput input)
    {
        var (t, recipe) = (input.Transition, input.Recipe);
        var target = recipe.State(t.To);
        return new WorkflowChange(input.Instance.Id, ChangeKind.Transitioned, input.Instance.Version + 1, input.At,
            From: t.From, To: t.To, On: t.On, Sha: input.Signal.Sha, Gates: input.Gates, Facts: input.Signal.Outputs, Loop: t.Loop,
            Findings: t.Loop is null ? null : input.Signal.Findings, Spend: input.Signal.Spend, Deadline: Deadlines.For(target, input.At),
            Completed: target?.Kind is StateKind.Terminal);
    }
}
