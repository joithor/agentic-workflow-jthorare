using Aw.Sindri.Core.Blocks.Evidence;

namespace Aw.Sindri.Testing;

/// <summary>What a ui-evidence tool says in a contract test.</summary>
/// <param name="Health">doctor's document.</param>
/// <param name="Provenance">provenance's document.</param>
/// <param name="RunPasses">Whether run and parity pass.</param>
/// <param name="Error">When set, every command fails with this ui-evidence error line (exit 4).</param>
public sealed record UiEvidenceScenario(StackHealth? Health = null, DatabaseProvenance? Provenance = null, bool RunPasses = true, string? Error = null);
