using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow;

/// <summary>How an instance ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstanceEnd>))]
public enum InstanceEnd
{
    /// <summary>It reached a terminal state.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed = 1,

    /// <summary>It was cancelled (by the human, or its item closed).</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled = 2,
}
