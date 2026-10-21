using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Pr;

/// <summary>'gate.pr.secret-scan' (PR babysitter spec §10.1, review finding X1): the lines and commit messages a push adds hold no secret shape. A hit names the file and line, never the match.</summary>
/// <param name="changes">What the workspace changed.</param>
/// <param name="scan">Finds secret shapes.</param>
internal sealed class SecretScanGate(IWorkspaceChanges changes, ISecretScan scan) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("gate.pr.secret-scan"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(scan);
        throw new NotSupportedException();
    }
}
