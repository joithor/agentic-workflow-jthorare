using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.ParkWorkflow;

/// <summary>Park an instance with a reason (active|wait → parked, §17.28.1).</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Reason">The reason.</param>
/// <param name="Detail">What the human should know.</param>
public sealed record ParkWorkflowRequest(InstanceId Instance, ParkReason Reason, string? Detail = null);
