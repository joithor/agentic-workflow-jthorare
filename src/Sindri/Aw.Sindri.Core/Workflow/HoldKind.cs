using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A hold on an instance that only the human lifts.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HoldKind>))]
public enum HoldKind
{
    /// <summary>Parked with a reason; 'unpark' returns it to its state.</summary>
    [JsonStringEnumMemberName("parked")]
    Parked = 1,

    /// <summary>Escalated (a gate failed on the top rung, a timeout said so, or a child escalated); 'unpark' returns it to its state.</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated = 2,
}
