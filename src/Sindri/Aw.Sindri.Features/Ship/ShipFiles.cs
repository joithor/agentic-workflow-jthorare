using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Ship;

/// <summary>What the pack and freeze Steps share: the draft's place in the run area, reading a packet's files and verifying a packet's hash.</summary>
internal static class ShipFiles
{
    /// <summary>The draft hand-off packet's directory below the instance's run area.</summary>
    internal const string DraftDirectory = "handoff-packet";

    /// <summary>Reads a JSON file of a packet directory.</summary>
    /// <param name="files">The reader.</param>
    /// <param name="dir">The packet directory.</param>
    /// <param name="name">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The JSON, or why it could not be read.</returns>
    internal static async ValueTask<(JsonNode? Value, string? Failure)> Json(IBriefFiles files, string dir, string name, CancellationToken cancellationToken)
    {
        var (text, failure) = await Text(files, dir, name, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return (null, failure);
        }

        try
        {
            return JsonNode.Parse(text) is { } node ? (node, null) : (null, $"{dir}/{name} holds null, not a JSON document");
        }
        catch (JsonException e)
        {
            return (null, $"{dir}/{name} is not JSON: {e.Message}");
        }
    }

    /// <summary>Reads a text file of a packet directory.</summary>
    /// <param name="files">The reader.</param>
    /// <param name="dir">The packet directory.</param>
    /// <param name="name">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or why it could not be read.</returns>
    internal static async ValueTask<(string? Text, string? Failure)> Text(IBriefFiles files, string dir, string name, CancellationToken cancellationToken)
    {
        var read = await files.Read(name, [dir], cancellationToken).ConfigureAwait(false);
        return read.IsError ? (null, $"{dir}/{name}: {read.Error.Message}") : (read.Value.Text, null);
    }

    /// <summary>Recomputes a packet's hash from its directory and compares it with the fact's (LC-1, SND-LINE-001).</summary>
    /// <param name="packets">aw-packets.</param>
    /// <param name="kind">The packet's kind.</param>
    /// <param name="packet">The fact.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>Null when it matches; otherwise why not.</returns>
    internal static async ValueTask<string?> Verify(IPackets packets, PacketKind kind, ShipPacket packet, CancellationToken cancellationToken)
    {
        var computed = await packets.Hash(new PacketRef(kind, packet.Dir), cancellationToken).ConfigureAwait(false);
        if (computed.IsError)
        {
            return $"aw-packets can't hash the {kind.ToString().ToLowerInvariant()} packet in {packet.Dir}: {computed.Error.Message}";
        }

        return string.Equals(computed.Value, packet.Hash, StringComparison.Ordinal)
            ? null
            : $"SND-LINE-001: the {kind.ToString().ToLowerInvariant()} packet in {packet.Dir} hashes to {computed.Value}, not the {packet.Hash} the fact names (fix: re-run the station that made it; a packet is never edited)";
    }
}
