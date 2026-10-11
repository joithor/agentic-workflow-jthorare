namespace Aw.Sindri.Features.QueryContext;

/// <summary>A context query (factory addendum §17.29.2): a question, optionally narrowed to some sources and a result count.</summary>
/// <param name="Question">The question as typed.</param>
/// <param name="Sources">Source names to search (code, notes, linear, connectai); null or empty, every source the profile turns on.</param>
/// <param name="Limit">How many results to return (1 to 50); null, the profile's context.limit.</param>
public sealed record QueryContextRequest(string Question, IReadOnlyList<string>? Sources = null, int? Limit = null);
