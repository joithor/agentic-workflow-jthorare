using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Context;

/// <summary>
/// Excerpts. Sources pick a raw line (<see cref="Around"/>, <see cref="Raw"/>: whole, up to <see cref="RawMaxLength"/>
/// characters); the engine scrubs it and only then cuts it for display (<see cref="Shown"/>), so a secret is never cut
/// short of the length its rule needs before the scrub sees it.
/// </summary>
public static partial class Snippets
{
    /// <summary>The longest excerpt shown, in characters.</summary>
    public const int MaxLength = 200;

    /// <summary>The longest raw line a source hands the engine, in characters.</summary>
    public const int RawMaxLength = 2000;

    /// <summary>What an excerpt that is one long encoded run (a key body, a token blob) is shown as.</summary>
    public const string EncodedBlob = "[REDACTED:encoded-blob]";

    /// <summary>The first line of <paramref name="text"/> that holds a term (ignoring case), else its first non-empty line, raw.</summary>
    /// <param name="text">The text.</param>
    /// <param name="terms">The query terms.</param>
    /// <returns>The line, trimmed and capped at <see cref="RawMaxLength"/>; empty when the text is.</returns>
    public static string Around(string text, IReadOnlyList<string> terms)
    {
        var lines = text.Split('\n');
        var line = lines.FirstOrDefault(l => terms.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase)))
            ?? lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
            ?? string.Empty;
        return Raw(line);
    }

    /// <summary>One raw line, trimmed and capped at <see cref="RawMaxLength"/> (the scrub runs on it before any display cut).</summary>
    /// <param name="line">The line.</param>
    /// <returns>The raw line.</returns>
    public static string Raw(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length <= RawMaxLength ? trimmed : trimmed[..RawMaxLength];
    }

    /// <summary>
    /// A scrubbed line as shown: one long unbroken run of base64 or hex-like characters (a private key's body line, a
    /// blob no pattern names) is withheld as <see cref="EncodedBlob"/>; anything else is cut to <see cref="MaxLength"/>.
    /// </summary>
    /// <param name="scrubbed">The line, already scrubbed.</param>
    /// <returns>The excerpt to show.</returns>
    public static string Shown(string scrubbed)
    {
        var trimmed = scrubbed.Trim();
        return Encoded.IsMatch(trimmed) ? EncodedBlob : Line(trimmed);
    }

    /// <summary>One line, trimmed and cut to <see cref="MaxLength"/> characters.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The excerpt.</returns>
    public static string Line(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length <= MaxLength ? trimmed : string.Concat(trimmed.AsSpan(0, MaxLength - 1), "…");
    }

    [GeneratedRegex("^[A-Za-z0-9+/=_-]{40,}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Encoded { get; }
}
