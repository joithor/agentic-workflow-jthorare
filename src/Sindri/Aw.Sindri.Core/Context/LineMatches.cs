namespace Aw.Sindri.Core.Context;

/// <summary>
/// Lexical ranking over lines, shared by the line-based sources (code, notes) and the fakes: a document ranks by how
/// many distinct terms it holds (in its name or its lines), then by how many of its lines match, then by name. Its
/// excerpt is the earliest line holding the most terms.
/// </summary>
/// <param name="terms">The query terms.</param>
public sealed class LineMatches(IReadOnlyList<string> terms)
{
    private readonly Dictionary<string, Match> _documents = new(StringComparer.Ordinal);

    /// <summary>Records one line of <paramref name="document"/>; a line with no term is ignored.</summary>
    /// <param name="document">The document's name or path (its terms count too).</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="text">The line.</param>
    public void Add(string document, int line, string text)
    {
        var held = Held(text);
        if (held.Count == 0)
        {
            return;
        }

        if (!_documents.TryGetValue(document, out var match))
        {
            match = new Match(Held(document));
            _documents[document] = match;
        }

        match.Terms.UnionWith(held);
        match.Lines++;
        if (held.Count > match.BestCount)
        {
            (match.BestCount, match.BestLine, match.BestText) = (held.Count, line, text);
        }
    }

    /// <summary>The documents, best first.</summary>
    /// <param name="limit">The most to return.</param>
    /// <returns>Each document with its excerpt line.</returns>
    public IReadOnlyList<(string Document, int Line, string Text)> Ranked(int limit) =>
    [
        .. _documents
            .OrderByDescending(d => d.Value.Terms.Count)
            .ThenByDescending(d => d.Value.Lines)
            .ThenBy(d => d.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(d => (d.Key, d.Value.BestLine, d.Value.BestText)),
    ];

    private HashSet<string> Held(string text) =>
        terms.Where(t => text.Contains(t, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.Ordinal);

    private sealed class Match(HashSet<string> nameTerms)
    {
        public HashSet<string> Terms { get; } = nameTerms;

        public int Lines { get; set; }

        public int BestCount { get; set; }

        public int BestLine { get; set; }

        public string BestText { get; set; } = string.Empty;
    }
}
