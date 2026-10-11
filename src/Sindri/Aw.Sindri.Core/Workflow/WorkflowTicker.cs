using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The timer tick (§17.28.1): due deadlines are applied like any other event, one commit per instance (with its
/// propagation), so a timeout is replayable and never a hidden side effect. After a clock jump the running timers move by
/// the gap first (design spec §9.2).
/// </summary>
/// <param name="store">The workflow store.</param>
/// <param name="loader">Loads instances with their recipes.</param>
/// <param name="builder">The commit builder.</param>
/// <param name="committer">Commits each change.</param>
/// <param name="clock">The clock.</param>
internal sealed class WorkflowTicker(IWorkflowStore store, InstanceLoader loader, CommitBuilder builder, Committer committer, TimeProvider clock)
{
    private const int Batch = 200;

    internal async ValueTask<Result<TickReport>> Tick(TimeSpan? clockJump, CancellationToken ct)
    {
        var extended = 0;
        if (clockJump is { } gap && gap > TimeSpan.Zero)
        {
            var moved = await Extend(gap, ct).ConfigureAwait(false);
            if (moved.IsError)
            {
                return moved.Error;
            }

            extended = moved.Value;
        }

        var due = await store.Due(clock.GetUtcNow(), Batch, ct).ConfigureAwait(false);
        if (due.IsError)
        {
            return due.Error;
        }

        var fired = 0;
        foreach (var instance in due.Value)
        {
            var applied = await Apply(instance, i => TimerDecisions.Due(i.Recipe.Recipe, i.Instance, clock.GetUtcNow()), $"timeout:{instance.State}", ct).ConfigureAwait(false);
            fired += applied.IsSuccess && applied.Value ? 1 : 0;
        }

        return new TickReport(fired, extended);
    }

    private async ValueTask<Result<int>> Extend(TimeSpan gap, CancellationToken ct)
    {
        var running = await store.List(new InstanceQuery(IncludeEnded: false, Max: 1000), ct).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        var moved = 0;
        foreach (var instance in running.Value.Where(i => i.Runs && i.Deadline is not null))
        {
            var applied = await Apply(instance, i => TimerDecisions.Extend(i.Instance, gap, clock.GetUtcNow()), "deadline-extended", ct).ConfigureAwait(false);
            moved += applied.IsSuccess && applied.Value ? 1 : 0;
        }

        return moved;
    }

    // One instance, one commit: a failure (a stale version, a changed recipe) skips it until the next tick.
    private async ValueTask<Result<bool>> Apply(WorkflowInstance instance, Func<LoadedInstance, WorkflowChange?> decide, string what, CancellationToken ct)
    {
        var loaded = await loader.WithRecipe(instance, ct).ConfigureAwait(false);
        if (loaded.IsError || decide(loaded.Value) is not { } change)
        {
            return loaded.IsError ? loaded.Error : false;
        }

        var draft = new CommitDraft();
        var (after, recorded) = builder.Record(draft, instance, change, WorkflowKeys.Change(instance, what));
        var done = await committer.Finish(draft, new AppliedChange(after, change, recorded), [], ct).ConfigureAwait(false);
        return done.IsError ? done.Error : !done.Value.AlreadyRecorded;
    }
}
