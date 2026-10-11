using Aw.Kernel;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>The ui-evidence tool (docs/csharp/evidence.md, Commands): the commands the evidence blocks run.</summary>
public interface IUiEvidence
{
    /// <summary>'ui-evidence doctor': the local stack's health.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Healthy or the failures, or SND-BLK-001.</returns>
    public ValueTask<Result<StackHealth>> Doctor(CancellationToken cancellationToken);

    /// <summary>'ui-evidence provenance': whether the local database holds only seed data.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The provenance and why, or SND-BLK-001.</returns>
    public ValueTask<Result<DatabaseProvenance>> Provenance(CancellationToken cancellationToken);

    /// <summary>'ui-evidence run': runs a UI script headless and writes summary.json in the run directory.</summary>
    /// <param name="run">The script, run directory, host and app build.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The run document (passed or failed), or SND-BLK-001 when the run produced none.</returns>
    public ValueTask<Result<EvidenceDocument>> Run(UiEvidenceRun run, CancellationToken cancellationToken);

    /// <summary>'ui-evidence parity': measures the design frames and writes parity.json in the run directory.</summary>
    /// <param name="run">The manifest, run directory, host and app build.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The parity document (passed or failed), or SND-BLK-001 when the run produced none.</returns>
    public ValueTask<Result<EvidenceDocument>> Parity(UiEvidenceRun run, CancellationToken cancellationToken);
}
