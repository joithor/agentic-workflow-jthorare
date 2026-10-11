using System.Text.RegularExpressions;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Testing.Context;

/// <summary>Scrubs GitHub token shapes (ghp_ and 36 more characters) like Aw.Redaction does; passes <see cref="TextScrubberContract"/>.</summary>
public sealed partial class FakeTextScrubber : ITextScrubber
{
    /// <inheritdoc/>
    public ScrubbedText Scrub(string text)
    {
        var scrubbed = Token.Replace(text, "[REDACTED:github-token]");
        return new ScrubbedText(scrubbed, !string.Equals(scrubbed, text, StringComparison.Ordinal));
    }

    [GeneratedRegex("gh[pousr]_[A-Za-z0-9]{36,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }
}
