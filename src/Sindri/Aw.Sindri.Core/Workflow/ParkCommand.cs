namespace Aw.Sindri.Core.Workflow;

/// <summary>Park the instance.</summary>
/// <param name="Reason">The reason.</param>
/// <param name="Detail">What the human should know.</param>
public sealed record ParkCommand(ParkReason Reason, string Detail) : WorkflowCommand;
