using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.snapshot' on the block runtime (PR babysitter spec §4.3, the lite slice): one read of the pull request, and the first outcome that
/// holds (<see cref="PrSnapshotRules"/>). It records 'fact:prState' on every outcome: the head sha it was read on and the failing checks with the sha they ran
/// on, which the CI-red loop reads. A red head also records 'fact:findings', the failing check names, so a round that fails the same way again is no progress.
/// </summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
internal sealed class PrSnapshotStep(IPullRequests pulls) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.snapshot");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulls);
        var none = new PullView(string.Empty, PullState.Open, string.Empty, Mergeable: null, ReviewDecision: null, UnresolvedThreads: 0, new PullChecks(PullChecksState.None, string.Empty, []), MergedSha: null);
        _ = PrSnapshotRules.Outcome(none);   // red stub: replaced by the real Step
        _ = PrSnapshotRules.ChecksOnHead(none);
        _ = PrSnapshotOutcomes.Green;
        _ = PrPaths.IsSource(string.Empty);
        throw new NotSupportedException();
    }
}
