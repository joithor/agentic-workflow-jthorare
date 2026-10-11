using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.CollectEvidence;

/// <summary>What the provider produced, and the engine's outcome when it was registered.</summary>
/// <param name="Kind">The evidence kind.</param>
/// <param name="Passed">Whether the run passed; only a passing run is registered.</param>
/// <param name="Detail">One line on the run.</param>
/// <param name="Digest">The document's digest.</param>
/// <param name="Document">The document's path.</param>
/// <param name="Registered">The registration's outcome, or null when the run failed and nothing was registered.</param>
public sealed record CollectEvidenceResponse(string Kind, bool Passed, string Detail, string Digest, string Document, WorkflowOutcome? Registered);
