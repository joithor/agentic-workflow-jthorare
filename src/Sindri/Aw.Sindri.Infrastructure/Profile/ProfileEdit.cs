using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The text edit that puts Sindri's managed Onyx entry into a profile (spec §4.1 step 9, D6): it finds `context`, `sources` and `onyx` by indentation, adds what is missing or
/// replaces a managed block, and leaves every other line, comment and the file's line endings alone. It then parses the file before and after (through <see cref="YamlToJson"/>)
/// and checks that nothing but `context.sources.onyx` changed. A shape it can't edit safely (flow style, anchors, aliases) or a hand-written `onyx` entry is refused with the snippet to paste.
/// </summary>
internal static partial class ProfileEdit
{
    /// <summary>Sets `context.sources.onyx` in <paramref name="yaml"/>.</summary>
    /// <param name="yaml">The profile text, or null or empty for none.</param>
    /// <param name="user">The `user` of a profile created from nothing.</param>
    /// <param name="port">The loopback port.</param>
    /// <param name="pointer">The token pointer text.</param>
    /// <param name="version">The pinned release tag.</param>
    /// <returns>The new text (the same text when the entry is already there), or SND-CLI-003.</returns>
    internal static Result<string> SetOnyxSource(string? yaml, string user, int port, string pointer, string version)
    {
        var entry = new Entry(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), pointer, version);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return "schemaVersion: 1\nuser: " + Scalar(user) + "\ncontext:\n  sources:\n" + string.Join('\n', entry.Block(indent: 4, unit: 2)) + "\n";
        }

        if (YamlToJson.TryConvert(yaml, out var before) is { } problem)
        {
            return Refuse($"the profile isn't valid YAML ({problem})", entry);
        }

        var existing = Find(JsonNode.Parse(before), "context", "sources", "onyx");
        if (existing is not null && entry.Matches(existing))
        {
            return yaml;
        }

        var lines = ProfileLines.Parse(yaml);
        var edited = Edit(lines, entry, existing);
        if (edited is { } why)
        {
            return Refuse(why, entry);
        }

        var text = lines.Render();
        return Verify(JsonNode.Parse(before), text, entry) is { } wrong ? Refuse(wrong, entry) : text;
    }

    // Null when the lines were edited; otherwise why they can't be.
    private static string? Edit(ProfileLines lines, Entry entry, JsonNode? existing)
    {
        if (lines.FindKey(0, lines.Lines.Count, indent: 0, "context") is not { } context)
        {
            lines.Lines.AddRange(["context:", "  sources:", .. entry.Block(indent: 4, unit: 2)]);
            return null;
        }

        if (context.Remainder.Length > 0)
        {
            return "`context` isn't a plain block map (flow style, an anchor, an alias or a value)";
        }

        var end = lines.BlockEnd(context.Index);
        var unit = (lines.ChildIndent(context.Index, end) ?? 2) - ProfileLines.Indent(lines.Lines[context.Index]);
        var child = lines.ChildIndent(context.Index, end) ?? unit;
        if (lines.FindKey(context.Index + 1, end, child, "sources") is not { } sources)
        {
            lines.Lines.InsertRange(lines.LastContent(context.Index, end) + 1, [new string(' ', child) + "sources:", .. entry.Block(child + unit, unit)]);
            return null;
        }

        return sources.Remainder.Length > 0 ? "`context.sources` isn't a plain block map (flow style, an anchor, an alias or a value)" : EditSources(lines, (sources.Index, child, unit), entry, existing);
    }

    private static string? EditSources(ProfileLines lines, (int Index, int Indent, int Unit) sources, Entry entry, JsonNode? existing)
    {
        var end = lines.BlockEnd(sources.Index);
        var child = lines.ChildIndent(sources.Index, end) ?? (sources.Indent + sources.Unit);
        if (lines.FindKey(sources.Index + 1, end, child, "onyx") is not { } onyx)
        {
            lines.Lines.InsertRange(lines.LastContent(sources.Index, end) + 1, entry.Block(child, sources.Unit));
            return null;
        }

        if (onyx.Remainder.Length > 0 || existing?["managed"]?.GetValueKind() != System.Text.Json.JsonValueKind.True)
        {
            return "`context.sources.onyx` is already set and isn't Sindri's managed entry (`managed: true`); Sindri never replaces one you wrote";
        }

        lines.Lines.RemoveRange(onyx.Index, lines.BlockEnd(onyx.Index) - onyx.Index);
        lines.Lines.InsertRange(onyx.Index, entry.Block(child, sources.Unit));
        return null;
    }

    // Null when the edited text is YAML, holds the entry, and differs from the old only there.
    private static string? Verify(JsonNode? before, string text, Entry entry)
    {
        if (YamlToJson.TryConvert(text, out var after) is not null)
        {
            return "the edited profile didn't parse, so it was not written";
        }

        var edited = JsonNode.Parse(after);
        if (!entry.Matches(Find(edited, "context", "sources", "onyx")))
        {
            return "the edited profile doesn't hold the entry as intended, so it was not written";
        }

        return JsonNode.DeepEquals(Without(before), Without(edited)) ? null : "the edit would change more than `context.sources.onyx`, so it was not written";
    }

    // The document without `context.sources.onyx`, and without the `sources` and `context` that held only it (the edit may have made them).
    private static JsonNode? Without(JsonNode? document)
    {
        var copy = JsonNode.Parse(document?.ToJsonString() ?? "null");
        if (copy is not JsonObject root)
        {
            return copy;
        }

        if (root.TryGetPropertyValue("context", out var held) && held is null)
        {
            _ = root.Remove("context");   // `context:` with nothing under it reads as null; the edit gives it a body
        }

        if (Find(root, "context", "sources") is JsonObject sources)
        {
            _ = sources.Remove("onyx");
            if (sources.Count == 0 && Find(root, "context") is JsonObject context)
            {
                _ = context.Remove("sources");
                if (context.Count == 0)
                {
                    _ = root.Remove("context");
                }
            }
        }

        return copy;
    }

    private static JsonNode? Find(JsonNode? node, params string[] path)
    {
        foreach (var key in path)
        {
            node = node is JsonObject map && map.TryGetPropertyValue(key, out var next) ? next : null;
        }

        return node;
    }

    private static string Scalar(string text) => PlainScalar.IsMatch(text) ? text : Quote(text);

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static Error Refuse(string why, Entry entry) => SindriErrors.InvalidInput.With(
        $"couldn't put Onyx into the profile: {why}. The profile is unchanged. Add this under `context.sources` yourself, then run `sindri onyx up` again:\n{string.Join('\n', entry.Block(indent: 0, unit: 2))}");

    [GeneratedRegex(@"^[A-Za-z0-9._-]+\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlainScalar { get; }

    // The values of the managed entry and the lines that write it.
    private sealed record Entry(string Url, string Token, string Version)
    {
        internal IReadOnlyList<string> Block(int indent, int unit)
        {
            var margin = new string(' ', indent);
            var inner = new string(' ', indent + unit);
            return [$"{margin}onyx:", $"{inner}url: {Quote(Url)}", $"{inner}token: {Quote(Token)}", $"{inner}version: {Quote(Version)}", $"{inner}managed: true"];
        }

        // Exactly these four keys with these values.
        internal bool Matches(JsonNode? node)
            => node is JsonObject map && map.Count == 4
                && string.Equals((string?)map["url"], Url, StringComparison.Ordinal)
                && string.Equals((string?)map["token"], Token, StringComparison.Ordinal)
                && string.Equals((string?)map["version"], Version, StringComparison.Ordinal)
                && map["managed"]?.GetValueKind() == System.Text.Json.JsonValueKind.True;
    }
}
