using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Timers (§17.28.1): a deadline on the row, evaluated by the tick like any other event. Nothing fires while the
/// instance is ended, parked, escalated or taken over (§9.3: takeover pauses timers).
/// </summary>
public static class TimerDecisions
{
    /// <summary>The change a due timer makes, or null when nothing is due.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <param name="instance">The instance.</param>
    /// <param name="now">The tick's time.</param>
    /// <returns>A park (timeout), an escalation, a retry, or null.</returns>
    public static WorkflowChange? Due(Recipe recipe, WorkflowInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(instance);
        if (!instance.Runs || instance.Deadline is not { } deadline || deadline > now)
        {
            return null;
        }

        var state = recipe.State(instance.State);
        var version = instance.Version + 1;
        var detail = $"'{instance.State}' timed out after {state?.Timeout}";
        return (state?.OnTimeout ?? TimeoutAction.Park, instance.Retries) switch
        {
            (TimeoutAction.Retry, 0) => new WorkflowChange(instance.Id, ChangeKind.Retried, version, now, Deadline: Deadlines.For(state, now), Detail: $"{detail}; retrying once"),
            (TimeoutAction.Escalate, _) => new WorkflowChange(instance.Id, ChangeKind.Escalated, version, now,
                Hold: new InstanceHold(HoldKind.Escalated, Reason: null, detail), Detail: detail),
            _ => new WorkflowChange(instance.Id, ChangeKind.Parked, version, now, Hold: new InstanceHold(HoldKind.Parked, ParkReason.Timeout, detail), Detail: detail),
        };
    }

    /// <summary>The change that moves a running timer by <paramref name="gap"/> after a clock jump (design spec §9.2), or null when no timer runs.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="gap">The jump.</param>
    /// <param name="now">The tick's time.</param>
    /// <returns>The change, or null.</returns>
    public static WorkflowChange? Extend(WorkflowInstance instance, TimeSpan gap, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return instance.Runs && instance.Deadline is { } deadline
            ? new WorkflowChange(instance.Id, ChangeKind.DeadlineExtended, instance.Version + 1, now, Deadline: deadline + gap, Detail: $"clock jumped {gap}")
            : null;
    }
}
