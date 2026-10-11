namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>List workflow instances, newest first.</summary>
/// <param name="All">Include completed and cancelled instances.</param>
public sealed record ListWorkflowsRequest(bool All = false);
