using Aw.Kernel;

namespace Aw.Sindri.Core.Blocks;

/// <summary>
/// An evidence provider (factory addendum §17.11, §17.28.2): an evidence block (kind <see cref="BlockKind.Evidence"/>, id
/// 'evidence.&lt;kind&gt;') that can also produce its evidence. It runs outside any engine transaction; the caller registers a
/// passing result through the engine (RegisterEvidenceCommand), so long tool runs never hold the ledger's write lock.
/// </summary>
public interface IEvidenceProvider : IBlock
{
    /// <summary>Produces the evidence once.</summary>
    /// <param name="request">The instance, the sha the evidence binds to, and the provider's inputs by name.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run produced (passed or not), or an error when it produced nothing usable.</returns>
    public ValueTask<Result<ProvidedEvidence>> Provide(EvidenceRequest request, CancellationToken cancellationToken);
}
