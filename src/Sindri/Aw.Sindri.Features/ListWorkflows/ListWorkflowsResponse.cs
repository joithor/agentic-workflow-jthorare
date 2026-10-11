using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>The instances.</summary>
/// <param name="Instances">Newest first.</param>
public sealed record ListWorkflowsResponse(IReadOnlyList<WorkflowInstance> Instances);
