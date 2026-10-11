using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>'gate.evidence.provenance' (rung 1): passes when 'ui-evidence provenance' says the local database holds only seed data.</summary>
/// <param name="tool">ui-evidence.</param>
internal sealed class ProvenanceGate(IUiEvidence tool) : IGateBlock
{
    /// <inheritdoc/>
    public BlockSpec Spec { get; } = new(BlockId.From("gate.evidence.provenance"), BlockKind.Gate, "ui-evidence provenance: the local database holds only seed data", [], [],
        Rung: GateRung.Deterministic);

    /// <inheritdoc/>
    public async ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        var finding = await tool.Provenance(cancellationToken).ConfigureAwait(false);
        if (finding.IsError)
        {
            return finding.Error;
        }

        var seeded = finding.Value.Provenance is DataProvenance.Seeded;
        return new GateVerdict(seeded, $"ui-evidence provenance: {(seeded ? "seeded" : "unknown")} ({finding.Value.Reason})");
    }
}
