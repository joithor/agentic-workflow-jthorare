using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Applies a universal command. Only unpark needs the instance's recipe (for the state's timer), so a stranded instance
/// can still be cancelled, parked, taken over, handed back and given evidence. Cancelling cancels the whole subtree in
/// the same commit. An unpark or handback that lets a parent run again applies the 'child.done' it missed. Registering
/// evidence that is already registered (same kind, sha and digest) records nothing.
/// </summary>
/// <param name="store">The workflow store (for children).</param>
/// <param name="builder">The commit builder.</param>
/// <param name="parents">Decides the 'child.done' a parent missed.</param>
/// <param name="propagation">Records a parent's change with its effects and child start.</param>
/// <param name="clock">The clock.</param>
internal sealed class CommandApplier(IWorkflowStore store, CommitBuilder builder, ParentDecisions parents, ParentPropagation propagation, TimeProvider clock)
{
    internal async ValueTask<Result<(CommitDraft Draft, IReadOnlyList<AppliedChange> Changes)?>> Decide(WorkflowInstance instance, Recipe? recipe, WorkflowCommand command,
        CancellationToken ct)
    {
        if (command is RegisterEvidenceCommand e && instance.Evidence.Any(r => r.Kind == e.Kind && r.Sha == e.Sha && r.Digest == e.Digest))
        {
            return Results.Success<(CommitDraft, IReadOnlyList<AppliedChange>)?>(value: null);
        }

        var decided = Decision(instance, recipe, command, clock.GetUtcNow());
        if (decided.IsError)
        {
            return decided.Error;
        }

        var draft = new CommitDraft();
        var change = decided.Value;
        var applied = builder.Record(draft, instance, change, WorkflowKeys.Change(instance, change.EventType));
        var changes = new List<AppliedChange> { new(applied.After, change, applied.Event) };
        var more = change.Kind is ChangeKind.Cancelled
            ? await CancelSubtree(draft, instance, applied.Event, depth: 1, ct).ConfigureAwait(false)
            : await CatchUp(draft, applied, recipe, changes, ct).ConfigureAwait(false);
        return more.IsError ? more.Error : (draft, changes);
    }

    private static Result<WorkflowChange> Decision(WorkflowInstance instance, Recipe? recipe, WorkflowCommand command, DateTimeOffset now) => command switch
    {
        CancelCommand c => CommandDecisions.Cancel(instance, c.Reason, now),
        ParkCommand p => CommandDecisions.Park(instance, new InstanceHold(HoldKind.Parked, p.Reason, p.Detail), now),
        UnparkCommand u => CommandDecisions.Unpark(recipe ?? throw new InvalidOperationException("Unpark needs the instance's recipe"), instance, u.Extra, now),
        TakeOverCommand => CommandDecisions.TakeOver(instance, now),
        HandBackCommand => CommandDecisions.HandBack(instance, now),
        RegisterEvidenceCommand e => CommandDecisions.Register(instance, new EvidenceRef(e.Kind, e.Sha, e.Digest, now), now),
        _ => throw new InvalidOperationException($"unknown workflow command {command.GetType().Name}"),
    };

    private async ValueTask<Result<Unit>> CancelSubtree(CommitDraft draft, WorkflowInstance parent, EventEnvelope<string> cause, int depth, CancellationToken ct)
    {
        var children = await store.Children(parent.Id, ct).ConfigureAwait(false);
        if (children.IsError)
        {
            return children.Error;
        }

        foreach (var child in children.Value)
        {
            var (current, caused) = child.Ended is null
                ? builder.Record(draft, child, new WorkflowChange(child.Id, ChangeKind.Cancelled, child.Version + 1, clock.GetUtcNow(),
                    Detail: $"parent {parent.Id.Value} was cancelled"), WorkflowKeys.Change(child, "workflow.cancelled"), cause)
                : (child, cause);
            var deeper = depth < ParentPropagation.MaxDepth
                ? await CancelSubtree(draft, current, caused, depth + 1, ct).ConfigureAwait(false)
                : Unit.Value;
            if (deeper.IsError)
            {
                return deeper.Error;
            }
        }

        return Unit.Value;
    }

    // An unpark or handback of a parent whose child completed meanwhile: apply the 'child.done' it missed.
    private async ValueTask<Result<Unit>> CatchUp(CommitDraft draft, (WorkflowInstance After, EventEnvelope<string> Event) applied, Recipe? recipe, List<AppliedChange> changes,
        CancellationToken ct)
    {
        if (recipe is null || changes[0].Change.Kind is not (ChangeKind.Unparked or ChangeKind.HandedBack))
        {
            return Unit.Value;
        }

        var children = await store.Children(applied.After.Id, ct).ConfigureAwait(false);
        if (children.IsError)
        {
            return children.Error;
        }

        if (await parents.CatchUp(applied.After, recipe, children.Value, ct).ConfigureAwait(false) is not { } done)
        {
            return Unit.Value;
        }

        var recorded = await propagation.Apply(draft, (applied.After, recipe), done, applied.Event, ct).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        changes.Add(new AppliedChange(recorded.Value.After, done.Change, recorded.Value.Event));
        return Unit.Value;
    }
}
