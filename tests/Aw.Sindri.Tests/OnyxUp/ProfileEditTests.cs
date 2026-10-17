using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.OnyxUp;

// The text edit that puts Sindri's managed Onyx entry into a profile and keeps everything else as it was.
[Lane(TestLane.Unit)]
public sealed class ProfileEditTests
{
    private const string Pointer = "keychain:onyx/sindri-onyx";
    private const string Version = "v4.9.0";

    private const string Before =
        "# Sindri profile for thor\n" +
        "schemaVersion: 1   # keep this\n" +
        "user: thor\n" +
        "\n" +
        "repos:   # my repos\n" +
        "  - name: web-app\n" +
        "    path: /Users/thor/web-app\n" +
        "# a comment between top-level keys\n" +
        "context:\n" +
        "  # context comment\n" +
        "  deadline: 4s\n" +
        "  sources:\n" +
        "    # sources comment\n" +
        "    linear: { teams: [ENG] }\n" +
        "services:\n" +
        "  nightly: { cadence: daily }";

    private const string After =
        "# Sindri profile for thor\n" +
        "schemaVersion: 1   # keep this\n" +
        "user: thor\n" +
        "\n" +
        "repos:   # my repos\n" +
        "  - name: web-app\n" +
        "    path: /Users/thor/web-app\n" +
        "# a comment between top-level keys\n" +
        "context:\n" +
        "  # context comment\n" +
        "  deadline: 4s\n" +
        "  sources:\n" +
        "    # sources comment\n" +
        "    linear: { teams: [ENG] }\n" +
        "    onyx:\n" +
        "      url: \"http://127.0.0.1:3002\"\n" +
        "      token: \"keychain:onyx/sindri-onyx\"\n" +
        "      version: \"v4.9.0\"\n" +
        "      managed: true\n" +
        "services:\n" +
        "  nightly: { cadence: daily }";

    private static string Set(string? yaml, int port = 3002) => ProfileEdit.SetOnyxSource(yaml, "thor", port, Pointer, Version).Value;

    [Fact]
    public void Creates_schemaVersion_user_and_the_section_for_an_empty_profile()
    {
        var text = Set(yaml: null);

        Assert.Equal(
            "schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:3002\"\n      token: \"keychain:onyx/sindri-onyx\"\n      version: \"v4.9.0\"\n      managed: true\n",
            text);
        Assert.Equal(text, Set(string.Empty));
    }

    [Fact]
    public void Keeps_every_comment_and_other_key_and_order() => Assert.Equal(After, Set(Before));

    [Fact]
    public void Adds_sources_under_an_existing_context()
    {
        var text = Set("schemaVersion: 1\nuser: thor\ncontext:\n  deadline: 4s\nservices: {}\n");

        Assert.Equal(
            "schemaVersion: 1\nuser: thor\ncontext:\n  deadline: 4s\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:3002\"\n      token: \"keychain:onyx/sindri-onyx\"\n      version: \"v4.9.0\"\n      managed: true\nservices: {}\n",
            text);
    }

    [Fact]
    public void Adds_context_at_the_end_of_a_profile_that_has_none()
    {
        var text = Set("schemaVersion: 1\nuser: thor\n");

        Assert.Equal(
            "schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:3002\"\n      token: \"keychain:onyx/sindri-onyx\"\n      version: \"v4.9.0\"\n      managed: true\n",
            text);
    }

    [Fact]
    public void Fills_an_empty_context_key()
    {
        var text = Set("schemaVersion: 1\nuser: thor\ncontext:\n");

        Assert.Equal(
            "schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:3002\"\n      token: \"keychain:onyx/sindri-onyx\"\n      version: \"v4.9.0\"\n      managed: true\n",
            text);
    }

    [Fact]
    public void Replaces_an_existing_managed_onyx_block_in_place()
    {
        var once = Set(Before, port: 3000);

        var moved = Set(once, port: 3002);

        Assert.Equal(After, moved);
        Assert.Equal(After, Set(After));   // the same entry again is the same text
    }

    [Theory]
    [InlineData("      url: \"http://127.0.0.1:3100\"\n      token: \"env:MY_ONYX\"\n      version: \"v4.9.0\"\n")]
    [InlineData("      url: \"http://127.0.0.1:3002\"\n      token: \"keychain:onyx/sindri-onyx\"\n      version: \"v4.9.0\"\n")]   // the same values but not marked managed
    public void Refuses_a_differing_unmanaged_onyx_entry_with_the_snippet_to_paste(string body)
    {
        var yaml = "schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n" + body;

        var error = ProfileEdit.SetOnyxSource(yaml, "thor", 3002, Pointer, Version).Error;

        Assert.Equal("SND-CLI-003", error.Code);
        Assert.Contains("managed: true", error.Message, StringComparison.Ordinal);
        Assert.Contains("url: \"http://127.0.0.1:3002\"", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("schemaVersion: 1\nuser: thor\ncontext: { deadline: 4s }\n")]
    [InlineData("schemaVersion: 1\nuser: thor\ncontext: &ctx\n  deadline: 4s\n")]
    [InlineData("schemaVersion: 1\nuser: thor\ncontext: *ctx\n")]
    [InlineData("schemaVersion: 1\nuser: thor\ncontext:\n  sources: { linear: { teams: [ENG] } }\n")]
    [InlineData("schemaVersion: 1\nuser: thor\ncontext:\n  sources: &s\n    linear: { teams: [ENG] }\n")]
    public void Refuses_flow_style_and_anchored_context_or_sources_with_the_snippet_and_changes_nothing(string yaml)
    {
        var error = ProfileEdit.SetOnyxSource(yaml, "thor", 3002, Pointer, Version).Error;

        Assert.Equal("SND-CLI-003", error.Code);
        Assert.Contains("context.sources", error.Message, StringComparison.Ordinal);
        Assert.Contains("managed: true", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_CRLF_and_a_missing_trailing_newline()
    {
        var crlf = Before.Replace("\n", "\r\n", StringComparison.Ordinal);   // no trailing newline

        var text = Set(crlf);

        Assert.Equal(After.Replace("\n", "\r\n", StringComparison.Ordinal), text);
        Assert.False(text.EndsWith('\n'));
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_a_trailing_newline_when_there_is_one() => Assert.Equal(After + "\n", Set(Before + "\n"));

    [Fact]
    public void Keeps_a_linear_source_beside_it_whichever_comes_first()
    {
        var onyxFirst = "schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:3000\"\n      token: \"keychain:onyx/sindri-onyx\"\n      version: \"v4.9.0\"\n      managed: true\n    linear: { teams: [ENG] }\n";

        var text = Set(onyxFirst);

        Assert.Equal(
            "schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:3002\"\n      token: \"keychain:onyx/sindri-onyx\"\n      version: \"v4.9.0\"\n      managed: true\n    linear: { teams: [ENG] }\n",
            text);
    }

    [Fact]
    public void Honors_a_four_space_indent()
    {
        var text = Set("schemaVersion: 1\nuser: thor\ncontext:\n    deadline: 4s\n");

        Assert.Equal(
            "schemaVersion: 1\nuser: thor\ncontext:\n    deadline: 4s\n    sources:\n        onyx:\n            url: \"http://127.0.0.1:3002\"\n            token: \"keychain:onyx/sindri-onyx\"\n            version: \"v4.9.0\"\n            managed: true\n",
            text);
    }

    [Fact]
    public void The_result_parses_to_the_same_JSON_except_the_onyx_entry()
    {
        var after = Set(Before);

        Assert.True(YamlToJson.TryConvert(Before, out var beforeJson) is null);
        Assert.True(YamlToJson.TryConvert(after, out var afterJson) is null);
        var (old, current) = (JsonNode.Parse(beforeJson)!, JsonNode.Parse(afterJson)!);
        var entry = current["context"]!["sources"]!["onyx"]!;
        Assert.Equal(("http://127.0.0.1:3002", Pointer, Version, true), ((string?)entry["url"], (string?)entry["token"], (string?)entry["version"], (bool?)entry["managed"]));
        _ = current["context"]!["sources"]!.AsObject().Remove("onyx");
        Assert.True(JsonNode.DeepEquals(old, current));
    }

    [Fact]
    public void A_profile_that_is_not_YAML_is_refused_untouched()
    {
        var error = ProfileEdit.SetOnyxSource("schemaVersion: [1\nuser: thor", "thor", 3002, Pointer, Version).Error;

        Assert.Equal("SND-CLI-003", error.Code);
    }
}
