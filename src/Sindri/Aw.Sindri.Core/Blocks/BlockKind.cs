using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks;

/// <summary>What a building block is (factory addendum §17.28.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlockKind>))]
public enum BlockKind
{
    /// <summary>A check that must pass before a transition is applied.</summary>
    [JsonStringEnumMemberName("gate")]
    Gate = 1,

    /// <summary>An evidence kind a transition can require (§17.11).</summary>
    [JsonStringEnumMemberName("evidence")]
    Evidence = 2,

    /// <summary>A Step implementation: the job, session or skill that does the work in an active state.</summary>
    [JsonStringEnumMemberName("step")]
    Step = 3,

    /// <summary>An outward action, emitted as an outbox command in the transition's transaction.</summary>
    [JsonStringEnumMemberName("effect")]
    Effect = 4,
}
