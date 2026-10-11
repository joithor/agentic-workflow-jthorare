using Aw.Kernel;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Finishes a command: propagates each change it made to the parent chain (in order, reading instances the draft already
/// changed from the draft), commits the draft under the held lease, and reads the instance back.
/// </summary>
/// <param name="store">The workflow store.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="propagation">Child → parent propagation.</param>
internal sealed class Committer(IWorkflowStore store, HeldLease held, ParentPropagation propagation)
{
    internal ValueTask<Result<WorkflowOutcome>> Finish(CommitDraft draft, AppliedChange trigger, IReadOnlyList<GateRecord> gates, CancellationToken ct) =>
        Finish(draft, [trigger], gates, ct);

    internal async ValueTask<Result<WorkflowOutcome>> Finish(CommitDraft draft, IReadOnlyList<AppliedChange> changes, IReadOnlyList<GateRecord> gates, CancellationToken ct)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        foreach (var change in changes)
        {
            var propagated = await propagation.Propagate(draft, change.After, change.Change, change.Event, ct).ConfigureAwait(false);
            if (propagated.IsError)
            {
                return propagated.Error;
            }
        }

        var subject = changes[0].After.Id;
        var after = draft.Latest(subject) ?? changes[^1].After;
        var committed = await store.Commit(draft.Build(), grant, ct).ConfigureAwait(false);
        if (committed.IsError)
        {
            return committed.Error;
        }

        if (!committed.Value.AlreadyRecorded)
        {
            return new WorkflowOutcome(after, AlreadyRecorded: false, gates);
        }

        var now = await store.Get(subject, ct).ConfigureAwait(false);
        return now.IsError ? now.Error : new WorkflowOutcome(now.Value ?? after, AlreadyRecorded: true, gates);
    }
}
