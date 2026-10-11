namespace Aw.Sindri.Core.Context;

/// <summary>Scrubs text before it leaves the engine (port; the adapter is Aw.Redaction): secrets become [REDACTED:rule].</summary>
public interface ITextScrubber
{
    /// <summary>Scrubs <paramref name="text"/>. A scrub that can't finish withholds the whole text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The scrubbed text and whether anything was replaced or withheld.</returns>
    public ScrubbedText Scrub(string text);
}
