using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The status column's words.</summary>
internal static class StatusWord
{
    internal static string Of(InstanceStatus status) => status switch
    {
        InstanceStatus.Active => "active",
        InstanceStatus.Parked => "parked",
        InstanceStatus.Escalated => "escalated",
        InstanceStatus.TakenOver => "taken-over",
        InstanceStatus.Done => "done",
        InstanceStatus.Cancelled => "cancelled",
        _ => "active",
    };
}
