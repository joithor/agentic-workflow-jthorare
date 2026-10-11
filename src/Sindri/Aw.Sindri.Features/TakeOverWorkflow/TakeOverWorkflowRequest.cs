using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.TakeOverWorkflow;

/// <summary>The human takes an instance over (design spec §9.3): timers pause.</summary>
/// <param name="Instance">The instance.</param>
public sealed record TakeOverWorkflowRequest(InstanceId Instance);
