using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow;

/// <summary>What a workflow event changed; the event type is 'workflow.' and this word.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChangeKind>))]
public enum ChangeKind
{
    /// <summary>The instance started ('workflow.started').</summary>
    [JsonStringEnumMemberName("started")]
    Started = 1,

    /// <summary>A recipe transition was applied ('workflow.transitioned').</summary>
    [JsonStringEnumMemberName("transitioned")]
    Transitioned = 2,

    /// <summary>It was parked ('workflow.parked').</summary>
    [JsonStringEnumMemberName("parked")]
    Parked = 3,

    /// <summary>A park or escalation was lifted ('workflow.unparked').</summary>
    [JsonStringEnumMemberName("unparked")]
    Unparked = 4,

    /// <summary>It was escalated ('workflow.escalated').</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated = 5,

    /// <summary>It was cancelled ('workflow.cancelled').</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled = 6,

    /// <summary>The human took it over ('workflow.taken-over').</summary>
    [JsonStringEnumMemberName("taken-over")]
    TakenOver = 7,

    /// <summary>The human handed it back ('workflow.handed-back').</summary>
    [JsonStringEnumMemberName("handed-back")]
    HandedBack = 8,

    /// <summary>A timeout restarted the state's timer once ('workflow.retried').</summary>
    [JsonStringEnumMemberName("retried")]
    Retried = 9,

    /// <summary>Evidence was registered ('workflow.evidence').</summary>
    [JsonStringEnumMemberName("evidence")]
    Evidence = 10,

    /// <summary>The timer moved after a clock jump ('workflow.deadline-extended').</summary>
    [JsonStringEnumMemberName("deadline-extended")]
    DeadlineExtended = 11,
}
