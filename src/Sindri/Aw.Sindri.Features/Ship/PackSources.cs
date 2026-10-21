using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Ship;

/// <summary>What the hand-off is assembled from: the phase packet's goal and kind, the proof pack's head, acceptance, hidden verdicts and not-proven, and the review's rounds.</summary>
/// <param name="PhaseId">The phase packet's id.</param>
/// <param name="Repo">'owner/name'.</param>
/// <param name="BaseSha">The phase packet's base commit.</param>
/// <param name="Goal">The phase's goal.</param>
/// <param name="Kind">The phase's kind.</param>
/// <param name="TestChange">True when the phase may change tests (phase.json's testChange is not empty).</param>
/// <param name="Head">The proof pack's head.</param>
/// <param name="Items">The proof pack's acceptance items, with their evidence.</param>
/// <param name="Hidden">The hidden scenarios' verdicts, without scenario ids.</param>
/// <param name="NotProven">not-proven.md.</param>
/// <param name="AcceptanceHash">The hash of the proof pack's acceptance.json, as its index.json records it.</param>
/// <param name="ReviewRounds">The number of review rounds.</param>
internal sealed record PackSources(string PhaseId, string Repo, string BaseSha, string Goal, string Kind, bool TestChange, string Head, IReadOnlyList<JsonObject> Items, IReadOnlyList<string> Hidden, string NotProven, string AcceptanceHash, int ReviewRounds)
{
    /// <summary>Reads the three packets' files.</summary>
    /// <param name="files">The reader.</param>
    /// <param name="phaseDir">The phase packet's directory.</param>
    /// <param name="proofDir">The proof pack's directory.</param>
    /// <param name="reviewDir">The review result's directory.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The sources, or why a file could not be read.</returns>
    internal static async ValueTask<(PackSources? Sources, string? Failure)> Load(IBriefFiles files, string phaseDir, string proofDir, string reviewDir, CancellationToken cancellationToken)
    {
        var docs = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var (dir, name) in new[] { (phaseDir, "packet.json"), (phaseDir, "phase.json"), (proofDir, "proof.json"), (proofDir, "index.json"), (proofDir, "acceptance.json"), (proofDir, "holdout.json"), (reviewDir, "review.json") })
        {
            var (doc, failure) = await ShipFiles.Json(files, dir, name, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return (null, failure);
            }

            docs[dir + "/" + name] = doc;
        }

        var (notProven, notProvenFailure) = await ShipFiles.Text(files, proofDir, "not-proven.md", cancellationToken).ConfigureAwait(false);
        return notProven is null
            ? (null, notProvenFailure)
            : From(docs, phaseDir, proofDir, reviewDir, notProven);
    }

    private static (PackSources? Sources, string? Failure) From(Dictionary<string, JsonNode> docs, string phaseDir, string proofDir, string reviewDir, string notProven)
    {
        var phase = docs[phaseDir + "/phase.json"];
        if (Ids(docs[phaseDir + "/packet.json"], phase) is not { } ids)
        {
            return (null, $"{phaseDir}: packet.json must name the packet's id, repo and baseSha, and phase.json its goal");
        }

        var head = ShipPacket.Text(docs[proofDir + "/proof.json"], "head");
        var acceptanceHash = ShipPacket.Text(docs[proofDir + "/index.json"]["acceptance"], "self");
        if (head is null || acceptanceHash is null)
        {
            return (null, $"{proofDir}: proof.json must name the head, and index.json the hash of its acceptance.json");
        }

        var items = (docs[proofDir + "/acceptance.json"]["items"] as JsonArray ?? []).OfType<JsonObject>().Where(static i => ShipPacket.Text(i, "status") != "superseded").ToList();
        var hidden = (docs[proofDir + "/holdout.json"]["results"] as JsonArray ?? []).OfType<JsonObject>().Select(static r => ShipPacket.Text(r, "verdict") ?? "unknown").ToList();
        var rounds = (docs[reviewDir + "/review.json"]["rounds"] as JsonArray ?? []).Count;
        var testChange = (phase["testChange"] as JsonArray ?? []).Count > 0;
        return (new PackSources(ids.Id, ids.Repo, ids.BaseSha, ids.Goal, ShipPacket.Text(phase, "kind") ?? string.Empty, testChange, head, items, hidden, notProven, acceptanceHash, rounds), null);
    }

    private sealed record PhaseIds(string Id, string Repo, string BaseSha, string Goal);

    private static PhaseIds? Ids(JsonNode packet, JsonNode phase) =>
        ShipPacket.Text(packet, "id") is { } id && ShipPacket.Text(packet, "repo") is { } repo && ShipPacket.Text(packet, "baseSha") is { } baseSha && ShipPacket.Text(phase, "goal") is { } goal
            ? new PhaseIds(id, repo, baseSha, goal)
            : null;
}
