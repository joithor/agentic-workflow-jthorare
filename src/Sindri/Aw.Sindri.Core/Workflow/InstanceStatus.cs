using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow;

/// <summary>An instance's status as a word: what status lines and filters show.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstanceStatus>))]
public enum InstanceStatus
{
    /// <summary>Working or waiting in its state.</summary>
    [JsonStringEnumMemberName("active")]
    Active = 1,

    /// <summary>Parked.</summary>
    [JsonStringEnumMemberName("parked")]
    Parked = 2,

    /// <summary>Escalated.</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated = 3,

    /// <summary>Taken over by the human; timers paused.</summary>
    [JsonStringEnumMemberName("taken-over")]
    TakenOver = 4,

    /// <summary>Reached a terminal state.</summary>
    [JsonStringEnumMemberName("done")]
    Done = 5,

    /// <summary>Cancelled.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled = 6,
}
