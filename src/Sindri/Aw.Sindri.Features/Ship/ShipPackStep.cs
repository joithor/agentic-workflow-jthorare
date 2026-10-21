using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>
/// 'step.ship.pack' on the block runtime (line spec §7.5): assembles the hand-off draft, the evidence pack as the PR body. It verifies the phase packet, the proof pack and the review
/// result against the hashes the facts name (SND-LINE-001), reads their files and writes the five files of the hand-off packet to the instance's run area ('handoff-packet/'): pr.json
/// and body.md for the forge's PR-open, green.json from <c language="text">fact:green</c>, and the index over the packets before it. Title, labels and body pass the scrubber. The draft is
/// validated with aw-packets (kind handoff) but not frozen: <c language="text">step.ship.freeze</c> does that. 'packed' carries <c language="text">handoffDraft = {hash, dir}</c>. The Step
/// fails (and an unrouted failure parks the instance for the owner) when an input is missing, a packet does not hash to its fact, a file cannot be read or written, or aw-packets
/// refuses a draft Sindri assembled (the builder cannot fix it). A relative <c language="text">phasePacket.dir</c> is read under the repo's checkout.
/// </summary>
/// <param name="files">Reads the phase packet's, proof pack's and review result's files.</param>
/// <param name="area">The instance's run area, where the draft is written.</param>
/// <param name="packets">Verifies the packets and validates the draft.</param>
/// <param name="checkouts">Where the repo is checked out (the draft's base commit is checked in it).</param>
/// <param name="scrubber">Takes secrets out of the title, labels and body.</param>
internal sealed class ShipPackStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts, ITextScrubber scrubber) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.pack");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, "fact:phasePacket", "fact:proofPack", "fact:review", "fact:lineContext", "fact:green") is { Count: > 0 } missing)
        {
            return BlockStepResult.Failed($"step.ship.pack needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        if (context.Address is not { } address || context.Repo is not { } repo || await checkouts.Of(repo, cancellationToken).ConfigureAwait(false) is not { IsError: false } checkout)
        {
            return BlockStepResult.Failed("step.ship.pack needs the instance's address and repo, and a checkout of the repo");
        }

        var (inputs, failure) = await Inputs(context, address, checkout.Value, cancellationToken).ConfigureAwait(false);
        return inputs is null
            ? BlockStepResult.Failed($"step.ship.pack {failure}")
            : await Write(inputs, checkout.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<(HandoffInputs? Inputs, string? Failure)> Inputs(StepContext context, string address, string checkout, CancellationToken cancellationToken)
    {
        var phase = ShipPacket.Of(StepFacts.Read(context, "fact:phasePacket"));
        var proof = ShipPacket.Of(StepFacts.Read(context, "fact:proofPack"));
        var review = ShipPacket.Of(StepFacts.Read(context, "fact:review"));
        var line = HandoffLine.Of(StepFacts.Read(context, "fact:lineContext"));
        if (phase is null || proof is null || review is null || line is null || StepFacts.Read(context, "fact:green") is not JsonObject green)
        {
            return (null, "needs fact:phasePacket, fact:proofPack and fact:review to name a hash and dir, fact:lineContext a line and phase, and fact:green to be an object");
        }

        phase = phase with { Dir = Path.IsPathRooted(phase.Dir) ? phase.Dir : Path.Combine(checkout, phase.Dir) };
        foreach (var (kind, packet) in new[] { (PacketKind.Phase, phase), (PacketKind.Proof, proof), (PacketKind.Review, review) })
        {
            if (await ShipFiles.Verify(packets, kind, packet, cancellationToken).ConfigureAwait(false) is { } mismatch)
            {
                return (null, mismatch);
            }
        }

        var (sources, failure) = await PackSources.Load(files, phase.Dir, proof.Dir, review.Dir, cancellationToken).ConfigureAwait(false);
        return sources is null ? (null, $"can't read the packets: {failure}") : (new HandoffInputs(sources, line, address, [phase.Hash, proof.Hash, review.Hash], green), null);
    }

    private async ValueTask<BlockStepResult> Write(HandoffInputs inputs, string checkout, CancellationToken cancellationToken)
    {
        foreach (var (name, text) in HandoffPack.Files(inputs, scrubber))
        {
            var written = await area.Write(inputs.Address, $"{ShipFiles.DraftDirectory}/{name}", text, append: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return BlockStepResult.Failed($"step.ship.pack can't write the hand-off: {written.Error.Message}");
            }
        }

        var dir = area.PathFor(inputs.Address, ShipFiles.DraftDirectory);
        if (dir.IsError)
        {
            return BlockStepResult.Failed($"step.ship.pack can't place the hand-off: {dir.Error.Message}");
        }

        return await Validated(dir.Value, checkout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Validated(string dir, string checkout, CancellationToken cancellationToken)
    {
        var validation = await packets.Validate(new PacketRef(PacketKind.Handoff, dir, Repo: checkout), cancellationToken).ConfigureAwait(false);
        if (validation.IsError || !validation.Value.Passed)
        {
            return BlockStepResult.Failed(validation.IsError
                ? $"step.ship.pack can't validate the hand-off with aw-packets: {validation.Error.Message}"
                : $"step.ship.pack: aw-packets refuses the hand-off Sindri assembled, which the builder can't fix: {string.Join("; ", validation.Value.Failures.Select(static f => $"{f.Rule} {f.Subject}: {f.Message}"))}");
        }

        var hash = await packets.Hash(new PacketRef(PacketKind.Handoff, dir), cancellationToken).ConfigureAwait(false);
        return hash.IsError
            ? BlockStepResult.Failed($"step.ship.pack can't hash the hand-off with aw-packets: {hash.Error.Message}")
            : StepFacts.Done("packed", ("fact:handoffDraft", new JsonObject { ["hash"] = hash.Value, ["dir"] = dir }));
    }
}
