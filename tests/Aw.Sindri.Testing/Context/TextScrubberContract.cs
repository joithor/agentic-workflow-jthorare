using Aw.Sindri.Core.Context;
using Xunit;

namespace Aw.Sindri.Testing.Context;

/// <summary>The contract every <see cref="ITextScrubber"/> honours: clean text passes unchanged; a token shape never survives.</summary>
public abstract class TextScrubberContract
{
    /// <summary>A GitHub token shape, assembled so no scanner mistakes this file for a leak.</summary>
    public static string PlantedToken { get; } = "gh" + "p_" + "0123456789abcdefghij" + "ABCDEFGHIJ012345";

    /// <summary>The scrubber under test.</summary>
    /// <returns>The scrubber.</returns>
    protected abstract ITextScrubber Create();

    /// <summary>Clean text comes back unchanged and not marked redacted.</summary>
    [Fact]
    public void Clean_text_passes_unchanged()
    {
        var scrubbed = Create().Scrub("The host lease has an epoch.");

        Expect.Equal(new ScrubbedText("The host lease has an epoch.", Redacted: false), scrubbed);
    }

    /// <summary>A GitHub token shape is replaced by a placeholder and the text marked redacted.</summary>
    [Fact]
    public void A_token_shape_is_replaced()
    {
        var scrubbed = Create().Scrub($"deploy with {PlantedToken} today");

        Expect.True(scrubbed.Redacted);
        Expect.True(!scrubbed.Text.Contains(PlantedToken, StringComparison.Ordinal));
        Expect.True(scrubbed.Text.Contains("[REDACTED:", StringComparison.Ordinal));
    }
}
