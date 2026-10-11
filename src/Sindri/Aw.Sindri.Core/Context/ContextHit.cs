namespace Aw.Sindri.Core.Context;

/// <summary>One result of a context query: scrubbed and cited.</summary>
/// <param name="Rank">Its 1-based rank.</param>
/// <param name="Title">A one-line title.</param>
/// <param name="Snippet">A short excerpt.</param>
/// <param name="Score">Its fused (RRF) score.</param>
/// <param name="Redacted">True when the scrub replaced or withheld something in it.</param>
/// <param name="Cite">Its citation.</param>
public sealed record ContextHit(int Rank, string Title, string Snippet, double Score, bool Redacted, Citation Cite);
