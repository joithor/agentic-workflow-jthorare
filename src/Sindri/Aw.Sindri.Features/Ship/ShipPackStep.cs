using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Ship;

/// <summary>'step.ship.pack' on the block runtime (line spec §7.5): assembles the hand-off draft, the evidence pack as the PR body.</summary>
/// <param name="files">Reads the phase packet's, proof pack's and review result's files.</param>
/// <param name="area">The instance's run area, where the draft is written.</param>
/// <param name="packets">Verifies the packets and validates the draft.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
/// <param name="scrubber">Takes secrets out of the title, labels and body.</param>
internal sealed class ShipPackStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts, ITextScrubber scrubber) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.pack");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"step.ship.pack is not built yet ({files}, {area}, {packets}, {checkouts}, {scrubber})");
}
