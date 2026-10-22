using Aw.Sindri.Host.Commands.Scrub;

namespace Aw.Sindri.Tests.Commands.Scrub;

/// <summary>The hook text, what counts as sindri's own, and which binary it calls.</summary>
public sealed class PreCommitHookTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_hook_calls_the_binary_by_the_path_it_was_given_and_fails_closed_without_it()
    {
        var text = PreCommitHook.Text("/opt/it's/sindri");

        Assert.StartsWith("#!/bin/sh\n", text, StringComparison.Ordinal);
        Assert.Contains("SINDRI='/opt/it'\\''s/sindri'\n", text, StringComparison.Ordinal);
        Assert.Contains("\"$SINDRI\" scrub --staged || exit 1\n", text, StringComparison.Ordinal);
        Assert.Contains("refusing the commit", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("#!/bin/sh\n# sindri-pre-commit v3\nx\n", true)]
    [InlineData("#!/bin/sh\n# sindri-pre-commit v2\nx\n", true)]
    [InlineData("#!/bin/sh\n# sindri-scrub-pre-commit v1\r\nx\n", true)]
    [InlineData("#!/bin/sh\necho '# sindri-pre-commit v3'\n", false)]
    [InlineData("#!/bin/sh\nnpm test\n", false)]
    public void Only_a_whole_marker_line_makes_a_hook_sindris(string hook, bool ours) => Assert.Equal(ours, PreCommitHook.IsSindri(hook));

    [Fact, Trait("Lane", "unit")]
    public void The_binary_is_SINDRI_BIN_then_the_running_sindri_then_the_name()
    {
        Assert.Equal("/x/y", ScrubBinary.Path(static n => n == "SINDRI_BIN" ? "/x/y" : null, "/a/sindri"));
        Assert.Equal("/a/sindri", ScrubBinary.Path(static _ => null, "/a/sindri"));
        Assert.Equal("sindri", ScrubBinary.Path(static _ => null, "/usr/bin/dotnet"));
        Assert.Equal("sindri", ScrubBinary.Path(static _ => null, null));
    }
}
