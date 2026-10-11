namespace Aw.Sindri.Core.Context;

/// <summary>A source's part in one query.</summary>
/// <param name="Source">The source.</param>
/// <param name="State">Ok, skipped or failed.</param>
/// <param name="Hits">How many hits it returned.</param>
/// <param name="Detail">Why it was skipped or failed: an SND-CONTEXT code and message, or 'not asked for' (the one
/// uncoded reason: the source is on but --sources didn't name it); null when ok.</param>
public sealed record SourceReport(SourceKind Source, SourceState State, int Hits, string? Detail);
