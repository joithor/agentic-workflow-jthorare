using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Ship;

/// <summary>SHIP-SHA-001 (line spec §7.5): the hand-off, the GREEN gate, the proof pack and the review result name one head sha. The half that the branch head moved since needs git; <c language="text">effect.pr.open</c> checks it before it pushes.</summary>
internal static class ShipSha
{
    /// <summary>The rule's id.</summary>
    internal const string Rule = "SHIP-SHA-001";

    /// <summary>One finding per source whose head is not the hand-off's.</summary>
    /// <param name="pr">The hand-off's pr.json.</param>
    /// <param name="green">The hand-off's green.json.</param>
    /// <param name="proof">The proof pack's proof.json.</param>
    /// <param name="review">The review result's review.json.</param>
    /// <returns>The findings; empty when the heads agree.</returns>
    internal static IReadOnlyList<JsonObject> Findings(JsonNode pr, JsonNode green, JsonNode proof, JsonNode review)
    {
        var head = ShipPacket.Text(pr, "headSha");
        return [.. new[] { ("the green gate", ShipPacket.Text(green, "sha")), ("the proof pack", ShipPacket.Text(proof, "head")), ("the review result", ShipPacket.Text(review, "head")) }
            .Where(s => head is null || !string.Equals(s.Item2, head, StringComparison.Ordinal))
            .Select(s => Finding($"{s.Item1} names {s.Item2 ?? "no head"}, the hand-off names {head ?? "no head"}"))];
    }

    private static JsonObject Finding(string message) => new()
    {
        ["rule"] = Rule,
        ["item"] = null,
        ["message"] = message,
        ["fix"] = "send the phase back to prove and review on its current head",
        ["severity"] = "fail",
    };
}
