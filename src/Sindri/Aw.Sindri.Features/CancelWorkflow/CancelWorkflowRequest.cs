using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CancelWorkflow;

/// <summary>Cancel an instance (any → cancelled, §17.28.1), and its running children.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Reason">Why, for the record.</param>
public sealed record CancelWorkflowRequest(InstanceId Instance, string? Reason = null);
