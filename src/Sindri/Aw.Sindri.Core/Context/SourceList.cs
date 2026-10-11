namespace Aw.Sindri.Core.Context;

/// <summary>One source's answer: its hits, best first, and when it answered.</summary>
/// <param name="Source">The source.</param>
/// <param name="Hits">The hits.</param>
/// <param name="RetrievedAt">When the source answered.</param>
public sealed record SourceList(SourceKind Source, IReadOnlyList<SourceHit> Hits, DateTimeOffset RetrievedAt);
