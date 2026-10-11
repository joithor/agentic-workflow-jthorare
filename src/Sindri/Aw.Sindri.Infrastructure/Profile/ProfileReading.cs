using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>JSON bytes to a checked, hashed profile: AwJson for the shape, ProfileRules for the rest, the canonical JSON for the hash.</summary>
internal static class ProfileReading
{
    internal const string Fix = "fix: edit the file; keys and types are in docs/csharp/sindri.md";

    /// <summary>The checked profile from <paramref name="json"/>, with its hash.</summary>
    /// <param name="json">The profile as JSON.</param>
    /// <param name="source">The file it came from, for messages.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The loaded profile, or SND-PROFILE-002 naming the file, the key path and the fix.</returns>
    internal static Result<LoadedProfile> FromJson(ReadOnlySpan<byte> json, string source, JsonSerializerOptions options)
    {
        if (EmptyContextKey(json) is { } key)
        {
            return SindriErrors.ProfileInvalid.With($"{source}: context.{key}: has no value; {EmptyKeyFix(key)} ({Fix})");
        }

        var typed = AwJson.Deserialize<SindriProfile>(json, options);
        if (typed.IsError)
        {
            return SindriErrors.ProfileInvalid.With($"{source}: {ProfileErrors.Explain(typed.Error.Message, options)} ({Fix})");
        }

        var checkedProfile = ProfileRules.Check(typed.Value);
        if (checkedProfile.IsError)
        {
            return SindriErrors.ProfileInvalid.With($"{source}: {checkedProfile.Error.Message} ({Fix})");
        }

        var canonical = Canonical(checkedProfile.Value, options);
        return new LoadedProfile(checkedProfile.Value, ProfileHashing.Of(canonical), source);
    }

    private static string EmptyKeyFix(string key) => key switch
    {
        "notes" => "give it a path, for example notes: { path: ~/notes }",
        "connectai" => "give it a server and a tool, for example connectai: { server: connectai, tool: search }",
        "limit" => "give it a number from 1 to 50, or remove it",
        _ => $"give it a mapping ({key}: {{}} turns the source on with its defaults)",
    };

    /// <summary>
    /// A key under context: with no value ('code:' alone in YAML reads as null, which would leave the source off while the
    /// file says it is named). Returns the first such key, or null.
    /// </summary>
    private static string? EmptyContextKey(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        if (!JsonDocument.TryParseValue(ref reader, out var document))
        {
            return null;   // AwJson reports the parse error
        }

        using (document)
        {
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("context", out var context) && context.ValueKind == JsonValueKind.Object
                ? context.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Null).Select(p => p.Name).FirstOrDefault()
                : null;
        }
    }

    /// <summary>The canonical JSON of a checked profile: what the hash covers and what an approval snapshot stores.</summary>
    /// <param name="profile">The checked profile.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The UTF-8 JSON.</returns>
    internal static byte[] Canonical(SindriProfile profile, JsonSerializerOptions options) => AwJson.Serialize(profile, options);
}
