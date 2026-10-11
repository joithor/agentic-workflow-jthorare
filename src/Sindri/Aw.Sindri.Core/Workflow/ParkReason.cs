using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow;

/// <summary>Why an instance is parked (factory addendum §17.28.1, §17.24.6).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParkReason>))]
public enum ParkReason
{
    /// <summary>A loop reached its cap.</summary>
    [JsonStringEnumMemberName("rounds")]
    Rounds = 1,

    /// <summary>The token or turn budget is spent.</summary>
    [JsonStringEnumMemberName("budget")]
    Budget = 2,

    /// <summary>A round made no progress over the one before (§17.23.13).</summary>
    [JsonStringEnumMemberName("no-progress")]
    NoProgress = 3,

    /// <summary>A state's timeout fired.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout = 4,

    /// <summary>It waits for the human's answer.</summary>
    [JsonStringEnumMemberName("needs-answer")]
    NeedsAnswer = 5,

    /// <summary>The spec contradicts itself or the code (§17.24.6).</summary>
    [JsonStringEnumMemberName("spec-conflict")]
    SpecConflict = 6,
}
