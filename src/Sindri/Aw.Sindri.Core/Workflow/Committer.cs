using Aw.Kernel;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Core.Workflow;

/// <summary>Finishes a command: propagates the triggering change to the parent chain, commits the draft under the held lease, and reads the instance back.</summary>
/// <param name="store">The workflow store.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="propagation">Child → parent propagation.</param>
internal sealed class Committer(IWorkflowStore store, HeldLease held, ParentPropagation propagation)
{
    internal async ValueTask<Result<WorkflowOutcome>> Finish(CommitDraft draft, AppliedChange trigger, IReadOnlyList<GateRecord> gates, CancellationToken ct)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var propagated = await propagation.Propagate(draft, trigger.After, trigger.Change, trigger.Event, ct).ConfigureAwait(false);
        if (propagated.IsError)
        {
            return propagated.Error;
        }

        var committed = await store.Commit(draft.Build(), grant, ct).ConfigureAwait(false);
        if (committed.IsError)
        {
            return committed.Error;
        }

        if (!committed.Value.AlreadyRecorded)
        {
            return new WorkflowOutcome(trigger.After, AlreadyRecorded: false, gates);
        }

        var now = await store.Get(trigger.After.Id, ct).ConfigureAwait(false);
        return now.IsError ? now.Error : new WorkflowOutcome(now.Value ?? trigger.After, AlreadyRecorded: true, gates);
    }
}
