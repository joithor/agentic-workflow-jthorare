using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// What a child's change means for its parent: completed → the parent's 'child.done' signal (gates apply, and a
/// top-rung failure escalates the parent); parked → parked with the child's reason; escalated → escalated; unparked →
/// the parent's hold from that child lifts; cancelled → parked 'needs-answer'. A parent that isn't running is left
/// alone; when it runs again (unpark, handback) it catches up on a child that finished meanwhile (<see cref="CatchUp"/>).
/// A parent whose recipe changed takes holds but no recipe decisions.
/// </summary>
/// <param name="blocks">The block registry.</param>
/// <param name="gates">The gate evaluator.</param>
/// <param name="clock">The clock.</param>
internal sealed class ParentDecisions(IBlockRegistry blocks, GateEvaluator gates, TimeProvider clock)
{
    internal async ValueTask<ParentChange?> For(WorkflowInstance parent, Recipe? recipe, WorkflowInstance child, WorkflowChange change, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (change.Kind is ChangeKind.Unparked)
        {
            return parent.Hold?.Child == child.Id && recipe is not null ? Unpark(parent, recipe, child, now) : null;
        }

        if (!parent.Runs)
        {
            return null;
        }

        if (change.Kind is ChangeKind.Transitioned)
        {
            return change.Completed && recipe is not null ? await Done(parent, recipe, ct).ConfigureAwait(false) : null;
        }

        var hold = HoldFor(change, child);
        return hold is null ? null : Hold(parent, change, hold, now);
    }

    /// <summary>The 'child.done' a parent missed while it was held or taken over: its state waits on a child, and that state's latest child completed.</summary>
    internal async ValueTask<ParentChange?> CatchUp(WorkflowInstance parent, Recipe recipe, IReadOnlyList<WorkflowInstance> children, CancellationToken ct)
    {
        if (!parent.Runs || recipe.State(parent.State)?.Child is not { } childRecipe)
        {
            return null;
        }

        var latest = children.Where(c => c.Recipe == childRecipe).MaxBy(c => c.Attempt);
        return latest?.Ended is InstanceEnd.Completed ? await Done(parent, recipe, ct).ConfigureAwait(false) : null;
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

    private static ParentChange? Unpark(WorkflowInstance parent, Recipe recipe, WorkflowInstance child, DateTimeOffset now)
    {
        var unparked = CommandDecisions.Unpark(recipe, parent, Spend.Zero, now);
        return unparked.IsError ? null : new ParentChange(unparked.Value with { Child = child.Id },
            WorkflowKeys.Change(parent, $"child:{child.Id.Value}:workflow.unparked"), Transition: null);
    }

    // The parent's 'child.done': the first candidate whose gates pass, or the escalation a failed top rung asks for.
    private async ValueTask<ParentChange?> Done(WorkflowInstance parent, Recipe recipe, CancellationToken ct)
    {
        var signal = new WorkflowSignal(Recipe.ChildDone, ExpectedVersion: null, Sha: null, new Dictionary<string, string>(StringComparer.Ordinal), [], EmptyDiff: false, Spend.Zero);
        var candidates = SignalDecisions.Candidates(recipe, parent, signal, blocks);
        if (candidates.IsError)
        {
            return null;
        }

        foreach (var transition in candidates.Value)
        {
            var key = WorkflowKeys.Transition(parent, transition.To, transition.On, sha: null);
            if (SignalDecisions.Missing(parent, transition, signal) is not null)
            {
                continue;
            }

            var records = await gates.Evaluate(parent, transition, signal, ct).ConfigureAwait(false);
            if (records.IsError)
            {
                continue;
            }

            var judged = GateOutcome.Judge(parent, records.Value, clock.GetUtcNow());
            if (judged is { IsError: false } escalation)
            {
                return new ParentChange(escalation.Value, key, Transition: null);
            }

            if (judged is not null)
            {
                continue;
            }

            var settled = SignalDecisions.Settle(new TransitionInput(parent, recipe, transition, signal, records.Value, clock.GetUtcNow()));
            return settled.IsError ? null : new ParentChange(settled.Value, key, settled.Value.Kind is ChangeKind.Transitioned ? transition : null);
        }

        return null;
    }
}
