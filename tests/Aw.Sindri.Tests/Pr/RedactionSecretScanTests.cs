using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Tests.Pr;

/// <summary>The secret scan over the one pattern set: the offsets of secret shapes, so the gate can name a line and never the match.</summary>
[Lane(TestLane.Unit)]
public sealed class RedactionSecretScanTests
{
    private const string _token = "ghp" + "_" + "0123456789abcdefghijABCDEFGHIJ012345";   // assembled here so this file holds no token-shaped text

    [Fact]
    public void Clean_text_has_no_hit() => Assert.Empty(new RedactionSecretScan().Hits("var x = 1;\nreturn x;"));

    [Fact]
    public void A_token_is_found_at_the_offset_it_starts()
    {
        var text = $"var a = 1;\nvar token = \"{_token}\";\n";

        Assert.Equal([text.IndexOf(_token, StringComparison.Ordinal)], new RedactionSecretScan().Hits(text));
    }

    [Fact]
    public void Two_tokens_are_two_hits_in_order()
    {
        var text = $"{_token}\nx\n{_token}";

        Assert.Equal([0, text.LastIndexOf(_token, StringComparison.Ordinal)], new RedactionSecretScan().Hits(text));
    }
}
