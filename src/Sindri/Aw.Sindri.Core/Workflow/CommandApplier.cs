using Aw.Kernel;
using Aw.Sindri.Core.Blocks;

namespace Aw.Sindri.Core.Workflow;

/// <summary>Applies a universal command; cancelling also cancels the instance's running children in the same commit.</summary>
/// <param name="store">The workflow store (for children).</param>
/// <param name="builder">The commit builder.</param>
/// <param name="clock">The clock.</param>
internal sealed class CommandApplier(IWorkflowStore store, CommitBuilder builder, TimeProvider clock)
{
    internal async ValueTask<Result<(CommitDraft Draft, AppliedChange Trigger)>> Decide(LoadedInstance loaded, WorkflowCommand command, CancellationToken ct)
    {
        var (instance, now) = (loaded.Instance, clock.GetUtcNow());
        var decided = command switch
        {
            CancelCommand c => CommandDecisions.Cancel(instance, c.Reason, now),
            ParkCommand p => CommandDecisions.Park(instance, new InstanceHold(HoldKind.Parked, p.Reason, p.Detail), now),
            UnparkCommand u => CommandDecisions.Unpark(loaded.Recipe.Recipe, instance, u.Extra, now),
            TakeOverCommand => CommandDecisions.TakeOver(instance, now),
            HandBackCommand => CommandDecisions.HandBack(instance, now),
            RegisterEvidenceCommand e => CommandDecisions.Register(instance, new EvidenceRef(e.Kind, e.Sha, e.Digest, now), now),
            _ => throw new InvalidOperationException($"unknown workflow command {command.GetType().Name}"),
        };
        if (decided.IsError)
        {
            return decided.Error;
        }

        var draft = new CommitDraft();
        var change = decided.Value;
        var applied = builder.Record(draft, instance, change, WorkflowKeys.Change(instance, change.EventType));
        if (change.Kind is ChangeKind.Cancelled)
        {
            var cascaded = await CancelChildren(draft, instance, applied, ct).ConfigureAwait(false);
            if (cascaded.IsError)
            {
                return cascaded.Error;
            }
        }

        return (draft, new AppliedChange(applied.After, change, applied.Event));
    }

    private async ValueTask<Result<Unit>> CancelChildren(CommitDraft draft, WorkflowInstance parent, (WorkflowInstance After, Kernel.Events.EventEnvelope<string> Event) cause,
        CancellationToken ct)
    {
        var children = await store.Children(parent.Id, ct).ConfigureAwait(false);
        if (children.IsError)
        {
            return children.Error;
        }

        foreach (var child in children.Value.Where(c => c.Ended is null))
        {
            var change = new WorkflowChange(child.Id, ChangeKind.Cancelled, child.Version + 1, clock.GetUtcNow(), Detail: $"parent {parent.Id.Value} was cancelled");
            _ = builder.Record(draft, child, change, WorkflowKeys.Change(child, change.EventType), cause.Event);
        }

        return Unit.Value;
    }
}
