using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks;

/// <summary>A gate's place on the decision ladder (§17.28.1): deterministic code first, then a judge question, then a bounded model call.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GateRung>))]
public enum GateRung
{
    /// <summary>Rung 1: deterministic code.</summary>
    [JsonStringEnumMemberName("deterministic")]
    Deterministic = 1,

    /// <summary>Rung 2: a judge question.</summary>
    [JsonStringEnumMemberName("judge")]
    Judge = 2,

    /// <summary>Rung 3, the top rung: a bounded model call. A failure here escalates (invariant 4).</summary>
    [JsonStringEnumMemberName("model")]
    Model = 3,
}
