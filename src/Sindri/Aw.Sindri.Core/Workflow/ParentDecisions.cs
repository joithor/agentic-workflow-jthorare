using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// What a child's change means for its parent: completed → the parent's 'child.done' signal (gates apply); parked →
/// parked with the child's reason; escalated → escalated; unparked → the parent's hold from that child lifts;
/// cancelled → parked 'needs-answer'. A parent that isn't running is left alone.
/// </summary>
/// <param name="blocks">The block registry.</param>
/// <param name="gates">The gate evaluator.</param>
/// <param name="clock">The clock.</param>
internal sealed class ParentDecisions(IBlockRegistry blocks, GateEvaluator gates, TimeProvider clock)
{
    internal async ValueTask<ParentChange?> For(LoadedInstance parent, WorkflowInstance child, WorkflowChange change, CancellationToken ct)
    {
        var (p, now) = (parent.Instance, clock.GetUtcNow());
        if (change.Kind is ChangeKind.Unparked)
        {
            return p.Hold?.Child == child.Id ? Unpark(parent, child, now) : null;
        }

        if (!p.Runs)
        {
            return null;
        }

        if (change.Kind is ChangeKind.Transitioned)
        {
            return change.Completed ? await Done(parent, ct).ConfigureAwait(false) : null;
        }

        var hold = HoldFor(change, child);
        return hold is null ? null : Hold(p, change, hold, now);
    }

    private static InstanceHold? HoldFor(WorkflowChange change, WorkflowInstance child)
    {
        var what = $"child {child.Id.Value}";
        return change.Kind switch
        {
            ChangeKind.Parked => new InstanceHold(HoldKind.Parked, change.Hold?.Reason, $"{what} parked ({WorkflowWords.Reason(change.Hold?.Reason)}): {change.Hold?.Detail}", Child: child.Id),
            ChangeKind.Escalated => new InstanceHold(HoldKind.Escalated, Reason: null, $"{what} escalated: {change.Hold?.Detail}", Child: child.Id),
            ChangeKind.Cancelled => new InstanceHold(HoldKind.Parked, ParkReason.NeedsAnswer, $"{what} was cancelled", Child: child.Id),
            ChangeKind.Started or ChangeKind.Transitioned or ChangeKind.Unparked or ChangeKind.TakenOver or ChangeKind.HandedBack or ChangeKind.Retried
                or ChangeKind.Evidence or ChangeKind.DeadlineExtended => null,
            _ => null,
        };
    }

    private static ParentChange Hold(WorkflowInstance parent, WorkflowChange childChange, InstanceHold hold, DateTimeOffset now)
    {
        var kind = hold.Kind is HoldKind.Parked ? ChangeKind.Parked : ChangeKind.Escalated;
        var change = new WorkflowChange(parent.Id, kind, parent.Version + 1, now, Hold: hold, Child: hold.Child, Detail: hold.Detail);
        return new ParentChange(change, WorkflowKeys.Change(parent, $"child:{hold.Child?.Value}:{childChange.EventType}"), Transition: null);
    }

    private static ParentChange? Unpark(LoadedInstance parent, WorkflowInstance child, DateTimeOffset now)
    {
        var unparked = CommandDecisions.Unpark(parent.Recipe.Recipe, parent.Instance, Spend.Zero, now);
        return unparked.IsError ? null : new ParentChange(unparked.Value with { Child = child.Id },
            WorkflowKeys.Change(parent.Instance, $"child:{child.Id.Value}:workflow.unparked"), Transition: null);
    }

    private async ValueTask<ParentChange?> Done(LoadedInstance parent, CancellationToken ct)
    {
        var (p, recipe) = (parent.Instance, parent.Recipe.Recipe);
        var signal = new WorkflowSignal(Recipe.ChildDone, ExpectedVersion: null, Sha: null, new Dictionary<string, string>(StringComparer.Ordinal), [], EmptyDiff: false, Spend.Zero);
        var candidates = SignalDecisions.Candidates(recipe, p, signal, blocks);
        if (candidates.IsError)
        {
            return null;
        }

        foreach (var transition in candidates.Value)
        {
            var records = await gates.Evaluate(p, transition, signal, ct).ConfigureAwait(false);
            if (records.IsError || GateOutcome.Judge(p, records.Value, clock.GetUtcNow()) is not null)
            {
                continue;
            }

            var settled = SignalDecisions.Settle(new TransitionInput(p, recipe, transition, signal, records.Value, clock.GetUtcNow()));
            return settled.IsError ? null : new ParentChange(settled.Value, WorkflowKeys.Transition(p, transition.To, transition.On, sha: null),
                settled.Value.Kind is ChangeKind.Transitioned ? transition : null);
        }

        return null;
    }
}
