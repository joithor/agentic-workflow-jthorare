using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Features.QueryContext;

/// <summary>A context query's answer: ranked, scrubbed, cited results and how each source did.</summary>
/// <param name="Question">The question as asked.</param>
/// <param name="Terms">The terms it was searched with.</param>
/// <param name="Results">The results, best first; each cites its source, locator and retrieval time.</param>
/// <param name="Sources">Each source the profile names: ok with its hit count, skipped or failed with the reason.</param>
public sealed record QueryContextResponse(
    string Question,
    IReadOnlyList<string> Terms,
    IReadOnlyList<ContextHit> Results,
    IReadOnlyList<SourceReport> Sources)
{
    /// <summary>True when a named source failed, so the results may miss what it holds.</summary>
    public bool Degraded => Sources.Any(s => s.State == SourceState.Failed);
}
