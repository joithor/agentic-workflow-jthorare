using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Pr;

/// <summary>'gate.pr.diff-scope' (PR babysitter spec §10.1, 'scope: phase'): what a fix changed stays inside the phase's allowed files and the tests, touches no CI config, deletes no test and skips none.</summary>
/// <param name="changes">What the workspace changed.</param>
/// <param name="files">Reads the phase packet's phase.json.</param>
internal sealed class DiffScopeGate(IWorkspaceChanges changes, IBriefFiles files) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("gate.pr.diff-scope"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(files);
        throw new NotSupportedException();
    }
}
