namespace Aw.Sindri.Core.Workflow;

/// <summary>Cancel the instance (and its running children).</summary>
/// <param name="Reason">Why.</param>
public sealed record CancelCommand(string Reason) : WorkflowCommand;
