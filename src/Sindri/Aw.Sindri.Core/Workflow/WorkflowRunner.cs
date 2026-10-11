using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>The engine: starts, signals, commands and ticks, each one commit through <see cref="Committer"/>.</summary>
/// <param name="catalog">The recipe catalog.</param>
/// <param name="loader">Loads instances with their recipes.</param>
/// <param name="builder">The commit builder.</param>
/// <param name="committer">Commits.</param>
/// <param name="signals">Decides signals.</param>
/// <param name="commands">Decides universal commands.</param>
/// <param name="ticker">Fires timers.</param>
internal sealed class WorkflowRunner(IRecipeCatalog catalog, InstanceLoader loader, CommitBuilder builder, Committer committer, SignalApplier signals,
    CommandApplier commands, WorkflowTicker ticker) : IWorkflowEngine
{
    public async ValueTask<Result<WorkflowOutcome>> Start(RecipeId recipe, ItemId item, int attempt, HeadSha? baseSha, CancellationToken cancellationToken)
    {
        if (attempt < 1)
        {
            return SindriErrors.TransitionNotAllowed.With("an attempt is a whole number from 1");
        }

        var found = await catalog.Get(recipe, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var draft = new CommitDraft();
        var change = builder.Start(new StartSpec(found.Value, item, attempt, Parent: null, baseSha));
        var (after, recorded) = builder.Record(draft, before: null, change, WorkflowKeys.Start(recipe, item, attempt));
        return await committer.Finish(draft, new AppliedChange(after, change, recorded), [], cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<WorkflowOutcome>> Signal(InstanceId id, WorkflowSignal signal, CancellationToken cancellationToken)
    {
        var loaded = await loader.Load(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var decided = await signals.Decide(loaded.Value, signal, cancellationToken).ConfigureAwait(false);
        if (decided.IsError)
        {
            return decided.Error;
        }

        return decided.Value is { } d
            ? await committer.Finish(d.Draft, d.Trigger, d.Gates, cancellationToken).ConfigureAwait(false)
            : new WorkflowOutcome(loaded.Value.Instance, AlreadyRecorded: true, []);
    }

    public async ValueTask<Result<WorkflowOutcome>> Apply(InstanceId id, WorkflowCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.Load(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var decided = await commands.Decide(loaded.Value, command, cancellationToken).ConfigureAwait(false);
        return decided.IsError
            ? decided.Error
            : await committer.Finish(decided.Value.Draft, decided.Value.Trigger, [], cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<Result<TickReport>> Tick(TimeSpan? clockJump, CancellationToken cancellationToken) => ticker.Tick(clockJump, cancellationToken);
}
