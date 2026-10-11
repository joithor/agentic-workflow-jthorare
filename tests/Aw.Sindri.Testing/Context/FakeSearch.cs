using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Testing.Context;

/// <summary>The fakes' search over seeded documents: Core's <see cref="LineMatches"/> over each title and body line.</summary>
public static class FakeSearch
{
    /// <summary>The documents matching <paramref name="terms"/>, best first.</summary>
    /// <param name="documents">The seed.</param>
    /// <param name="terms">The terms.</param>
    /// <param name="limit">The most to return.</param>
    /// <returns>Each matching document with its excerpt line (1-based; the title is line 0).</returns>
    public static IReadOnlyList<(SeedDocument Document, int Line, string Text)> Run(IReadOnlyList<SeedDocument> documents, IReadOnlyList<string> terms, int limit)
    {
        var matches = new LineMatches(terms);
        foreach (var document in documents)
        {
            matches.Add(document.Name, 0, document.Title);
            var lines = document.Body.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                matches.Add(document.Name, i + 1, lines[i]);
            }
        }

        return [.. matches.Ranked(limit).Select(m => (documents.First(d => d.Name == m.Document), m.Line, m.Text))];
    }
}
