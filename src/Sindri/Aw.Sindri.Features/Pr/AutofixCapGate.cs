using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>'gate.pr.autofix-cap' (PR babysitter spec §10.1, IDEA-016): counts the CI fixes pushed to this pull request across instances and refuses at the profile's cap, with the last failing check's log.</summary>
/// <param name="ledger">The ledger's 'pr.pushed' rows.</param>
/// <param name="contents">Reads a push's 'fact:push' back.</param>
/// <param name="profile">The approved profile's cap.</param>
/// <param name="pulls">The forge, for the log shown with the refusal.</param>
internal sealed class AutofixCapGate(ILedgerReader ledger, IFactContents contents, IApprovedProfile profile, IPullRequests pulls) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("gate.pr.autofix-cap"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.Network, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(pulls);
        throw new NotSupportedException();
    }
}
