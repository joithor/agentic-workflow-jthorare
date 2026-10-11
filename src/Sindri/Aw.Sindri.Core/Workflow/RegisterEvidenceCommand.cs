using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>Register evidence for the instance.</summary>
/// <param name="Kind">The evidence kind (block 'evidence.&lt;kind&gt;').</param>
/// <param name="Sha">The commit it is bound to.</param>
/// <param name="Digest">The artifact digest.</param>
public sealed record RegisterEvidenceCommand(string Kind, HeadSha Sha, string Digest) : WorkflowCommand;
