using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Context;

/// <summary>A context source, by what it reads (factory addendum §17.17 Q2: the sources reachable today).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceKind>))]
public enum SourceKind
{
    /// <summary>The profile repos' tracked files.</summary>
    [JsonStringEnumMemberName("code")]
    Code,

    /// <summary>A notes directory.</summary>
    [JsonStringEnumMemberName("notes")]
    Notes,

    /// <summary>Linear issues.</summary>
    [JsonStringEnumMemberName("linear")]
    Linear,

    /// <summary>ConnectAI, through an MCP tool.</summary>
    [JsonStringEnumMemberName("connectai")]
    ConnectAi,
}
