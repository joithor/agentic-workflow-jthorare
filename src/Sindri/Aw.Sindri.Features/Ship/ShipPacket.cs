using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Ship;

/// <summary>A packet a fact names (<c language="text">{ hash, dir }</c>).</summary>
/// <param name="Hash">The packet's canonical hash.</param>
/// <param name="Dir">The packet's directory.</param>
internal sealed record ShipPacket(string Hash, string Dir)
{
    /// <summary>Reads a packet fact; null when it names no hash or no directory.</summary>
    /// <param name="fact">The fact's JSON.</param>
    /// <returns>The packet.</returns>
    internal static ShipPacket? Of(JsonNode? fact) =>
        fact is JsonObject obj && Text(obj, "hash") is { } hash && Text(obj, "dir") is { } dir ? new ShipPacket(hash, dir) : null;

    /// <summary>The non-blank string an object holds under a key, or null.</summary>
    /// <param name="obj">The object.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text.</returns>
    internal static string? Text(JsonNode? obj, string key) =>
        obj?[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
