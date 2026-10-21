using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>'step.pr.ci-log' on the block runtime: the failing checks' logs, fenced, and the check that the workspace stands on the head the fix starts from.</summary>
/// <param name="pulls">The forge.</param>
/// <param name="diff">Reads the workspace's HEAD and whether it holds changes.</param>
internal sealed class PrCiLogStep(IPullRequests pulls, IRunDiff diff) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.ci-log");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulls);
        ArgumentNullException.ThrowIfNull(diff);
        throw new NotSupportedException();
    }
}
