namespace Aw.Sindri.Core.Context;

/// <summary>A hit after fusion.</summary>
/// <param name="Source">The source it came from.</param>
/// <param name="Hit">The raw hit.</param>
/// <param name="Score">Its RRF score.</param>
/// <param name="RetrievedAt">When its source answered.</param>
public sealed record FusedHit(SourceKind Source, SourceHit Hit, double Score, DateTimeOffset RetrievedAt);
