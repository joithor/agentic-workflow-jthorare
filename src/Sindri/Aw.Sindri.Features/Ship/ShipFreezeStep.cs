using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Ship;

/// <summary>'step.ship.freeze' on the block runtime (line spec §7.5, SHIP-SHA-001): proof, review and hand-off agree on one head, then the hand-off is machine-frozen.</summary>
/// <param name="files">Reads the proof pack's and review result's files.</param>
/// <param name="area">The instance's run area, where the draft is.</param>
/// <param name="packets">Verifies the packets and freezes the hand-off.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
internal sealed class ShipFreezeStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.freeze");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"step.ship.freeze is not built yet ({files}, {area}, {packets}, {checkouts})");
}
