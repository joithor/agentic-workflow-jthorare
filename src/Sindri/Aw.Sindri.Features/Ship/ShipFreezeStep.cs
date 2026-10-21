using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>
/// 'step.ship.freeze' on the block runtime (line spec §7.5, SHIP-SHA-001): the hand-off draft, the proof pack and the review result are verified against the hashes the facts name
/// (SND-LINE-001), and their heads must agree with the hand-off's and the GREEN gate's. A head that does not is 'stale' with one finding per source and nothing is frozen. Otherwise
/// the hand-off is machine-frozen through aw-packets (kind handoff) and 'frozen' carries <c language="text">handoff = {hash, dir}</c>. The Step fails (and an unrouted failure parks
/// the instance for the owner) when an input is missing, a packet does not hash to its fact, the draft is not this instance's, a file cannot be read, or aw-packets refuses the hand-off.
/// Whether the branch head moved since needs git: <c language="text">effect.pr.open</c> checks it before it pushes.
/// </summary>
/// <param name="files">Reads the proof pack's and review result's files.</param>
/// <param name="area">The instance's run area, where the draft is.</param>
/// <param name="packets">Verifies the packets and freezes the hand-off.</param>
/// <param name="checkouts">Where the repo is checked out (the hand-off's base commit is checked in it).</param>
internal sealed class ShipFreezeStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.freeze");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, "fact:handoffDraft", "fact:proofPack", "fact:review") is { Count: > 0 } missing)
        {
            return BlockStepResult.Failed($"step.ship.freeze needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        if (context.Address is not { } address || context.Repo is not { } repo || await checkouts.Of(repo, cancellationToken).ConfigureAwait(false) is not { IsError: false } checkout)
        {
            return BlockStepResult.Failed("step.ship.freeze needs the instance's address and repo, and a checkout of the repo");
        }

        var draft = ShipPacket.Of(StepFacts.Read(context, "fact:handoffDraft"));
        var proof = ShipPacket.Of(StepFacts.Read(context, "fact:proofPack"));
        var review = ShipPacket.Of(StepFacts.Read(context, "fact:review"));
        if (draft is null || proof is null || review is null)
        {
            return BlockStepResult.Failed("step.ship.freeze needs fact:handoffDraft, fact:proofPack and fact:review to name a hash and dir");
        }

        return await Freeze(address, (draft, proof, review), checkout.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Freeze(string address, (ShipPacket Draft, ShipPacket Proof, ShipPacket Review) packet, string checkout, CancellationToken cancellationToken)
    {
        var place = area.PathFor(address, ShipFiles.DraftDirectory);
        if (place.IsError || !string.Equals(place.Value, packet.Draft.Dir, StringComparison.Ordinal))
        {
            return BlockStepResult.Failed($"step.ship.freeze: the draft in {packet.Draft.Dir} is not this instance's hand-off");
        }

        foreach (var (kind, verified) in new[] { (PacketKind.Handoff, packet.Draft), (PacketKind.Proof, packet.Proof), (PacketKind.Review, packet.Review) })
        {
            if (await ShipFiles.Verify(packets, kind, verified, cancellationToken).ConfigureAwait(false) is { } mismatch)
            {
                return BlockStepResult.Failed($"step.ship.freeze {mismatch}");
            }
        }

        var (findings, failure) = await Heads(address, packet.Proof.Dir, packet.Review.Dir, cancellationToken).ConfigureAwait(false);
        if (findings is null)
        {
            return BlockStepResult.Failed($"step.ship.freeze can't read the heads: {failure}");
        }

        return findings.Count > 0
            ? StepFacts.Done("stale", ("fact:findings", new JsonArray([.. findings])))
            : await Frozen(packet.Draft.Dir, checkout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<(IReadOnlyList<JsonObject>? Findings, string? Failure)> Heads(string address, string proofDir, string reviewDir, CancellationToken cancellationToken)
    {
        var pr = await area.Read(address, $"{ShipFiles.DraftDirectory}/pr.json", cancellationToken).ConfigureAwait(false);
        var green = await area.Read(address, $"{ShipFiles.DraftDirectory}/green.json", cancellationToken).ConfigureAwait(false);
        if (pr.IsError || green.IsError)
        {
            return (null, (pr.IsError ? pr.Error : green.Error).Message);
        }

        var (proof, proofFailure) = await ShipFiles.Json(files, proofDir, "proof.json", cancellationToken).ConfigureAwait(false);
        var (review, reviewFailure) = await ShipFiles.Json(files, reviewDir, "review.json", cancellationToken).ConfigureAwait(false);
        try
        {
            return proof is null || review is null || JsonNode.Parse(pr.Value) is not { } prNode || JsonNode.Parse(green.Value) is not { } greenNode
                ? (null, proofFailure ?? reviewFailure ?? "pr.json or green.json of the draft is empty")
                : (ShipSha.Findings(prNode, greenNode, proof, review), null);
        }
        catch (System.Text.Json.JsonException e)
        {
            return (null, $"the draft's pr.json or green.json is not JSON: {e.Message}");
        }
    }

    private async ValueTask<BlockStepResult> Frozen(string dir, string checkout, CancellationToken cancellationToken)
    {
        var frozen = await packets.Freeze(new PacketRef(PacketKind.Handoff, dir, Repo: checkout), PacketFreezer.Machine, cancellationToken).ConfigureAwait(false);
        if (frozen.IsError)
        {
            return BlockStepResult.Failed($"step.ship.freeze can't freeze the hand-off with aw-packets: {frozen.Error.Message}");
        }

        return frozen.Value is { Frozen: true, Hash: { } hash }
            ? StepFacts.Done("frozen", ("fact:handoff", new JsonObject { ["hash"] = hash, ["dir"] = dir }))
            : BlockStepResult.Failed($"step.ship.freeze: aw-packets refused the hand-off Sindri assembled, which the builder can't fix: {string.Join("; ", frozen.Value.Failures.Select(static f => $"{f.Rule} {f.Subject}: {f.Message}"))}");
    }
}
