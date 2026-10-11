using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.HandBackWorkflow;

/// <summary>The human hands an instance back (design spec §9.3): timers resume with the time they had left.</summary>
/// <param name="Instance">The instance.</param>
public sealed record HandBackWorkflowRequest(InstanceId Instance);
