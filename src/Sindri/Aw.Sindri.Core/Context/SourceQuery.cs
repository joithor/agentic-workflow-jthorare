namespace Aw.Sindri.Core.Context;

/// <summary>What a source is asked: the question's terms and how many hits to return at most.</summary>
/// <param name="Terms">The search terms, lower case, at least one.</param>
/// <param name="Limit">The most hits to return.</param>
public sealed record SourceQuery(IReadOnlyList<string> Terms, int Limit);
