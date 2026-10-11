using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>A request to an <see cref="IEvidenceProvider"/>.</summary>
/// <param name="Instance">The instance the evidence is for.</param>
/// <param name="Sha">The commit the evidence binds to (passed to the tool as the app build).</param>
/// <param name="Inputs">The provider's inputs by name: exactly the names its <see cref="BlockSpec.Inputs"/> declare.</param>
public sealed record EvidenceRequest(InstanceId Instance, HeadSha Sha, IReadOnlyDictionary<string, string> Inputs);
