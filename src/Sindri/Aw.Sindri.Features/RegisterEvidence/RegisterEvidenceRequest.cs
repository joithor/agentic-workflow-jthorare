using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.RegisterEvidence;

/// <summary>Register a piece of evidence for an instance (§17.11): only registered evidence counts toward a transition.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Kind">The evidence kind (block evidence.&lt;kind&gt;).</param>
/// <param name="Sha">The commit it was produced on.</param>
/// <param name="Digest">The artifact digest.</param>
public sealed record RegisterEvidenceRequest(InstanceId Instance, string Kind, HeadSha Sha, string Digest);
