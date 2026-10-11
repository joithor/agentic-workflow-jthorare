namespace Aw.Sindri.Core.Workflow;

/// <summary>An instance's status in words, as messages and the CLI show it: 'parked (no-progress)', 'taken-over'.</summary>
public static class WorkflowWords
{
    /// <summary>The status words of <paramref name="instance"/>.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>For example 'active', 'parked (rounds)', 'escalated', 'done'.</returns>
    public static string Status(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return instance.Status switch
        {
            InstanceStatus.Parked => $"parked ({Reason(instance.Hold?.Reason)})",
            InstanceStatus.TakenOver => "taken-over",
            InstanceStatus.Escalated => "escalated",
            InstanceStatus.Done => "done",
            InstanceStatus.Cancelled => "cancelled",
            InstanceStatus.Active => "active",
            _ => "active",
        };
    }

    /// <summary>A park reason as its word.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>For example 'no-progress'.</returns>
    public static string Reason(ParkReason? reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.Budget => "budget",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Timeout => "timeout",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.SpecConflict => "spec-conflict",
        _ => "unknown",
    };
}
