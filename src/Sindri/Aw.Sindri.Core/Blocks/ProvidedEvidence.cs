namespace Aw.Sindri.Core.Blocks;

/// <summary>What an evidence provider's run produced.</summary>
/// <param name="Passed">Whether the run passed; only a passing run is registered as evidence.</param>
/// <param name="Digest">'sha256:&lt;hex&gt;' of the run's document, the digest registered with the evidence.</param>
/// <param name="Document">The absolute path of the run's document (summary.json, parity.json).</param>
/// <param name="Detail">One line on the outcome.</param>
public sealed record ProvidedEvidence(bool Passed, string Digest, string Document, string Detail);
