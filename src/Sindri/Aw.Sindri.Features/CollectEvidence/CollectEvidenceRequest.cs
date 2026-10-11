using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CollectEvidence;

/// <summary>Run an evidence provider for an instance and, when the run passes, register what it produced (§17.11).</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Kind">The evidence kind ('ui-browser' runs block evidence.ui-browser).</param>
/// <param name="Sha">The commit the evidence binds to.</param>
/// <param name="Inputs">The provider's inputs by name.</param>
public sealed record CollectEvidenceRequest(InstanceId Instance, string Kind, HeadSha Sha, IReadOnlyDictionary<string, string> Inputs);
