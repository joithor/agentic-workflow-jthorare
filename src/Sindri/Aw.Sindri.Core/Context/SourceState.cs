using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Context;

/// <summary>How a source did in one query.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceState>))]
public enum SourceState
{
    /// <summary>It answered (perhaps with no hits).</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>It wasn't searched: not asked for, or it can't open (SND-CONTEXT-002).</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>It didn't answer (SND-CONTEXT-010): the query went on without it, degraded.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
