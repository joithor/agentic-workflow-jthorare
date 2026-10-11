namespace Aw.Sindri.Core.Context;

/// <summary>Short excerpts: one line, at most <see cref="MaxLength"/> characters.</summary>
public static class Snippets
{
    /// <summary>The longest excerpt, in characters.</summary>
    public const int MaxLength = 200;

    /// <summary>The first line of <paramref name="text"/> that holds a term (ignoring case), else its first non-empty line.</summary>
    /// <param name="text">The text.</param>
    /// <param name="terms">The query terms.</param>
    /// <returns>The excerpt, trimmed; empty when the text is.</returns>
    public static string Around(string text, IReadOnlyList<string> terms)
    {
        var lines = text.Split('\n');
        var line = lines.FirstOrDefault(l => terms.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase)))
            ?? lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
            ?? string.Empty;
        return Line(line);
    }

    /// <summary>One line, trimmed and cut to <see cref="MaxLength"/> characters.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The excerpt.</returns>
    public static string Line(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length <= MaxLength ? trimmed : string.Concat(trimmed.AsSpan(0, MaxLength - 1), "…");
    }
}
