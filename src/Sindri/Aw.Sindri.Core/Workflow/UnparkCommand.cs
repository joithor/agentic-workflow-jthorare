namespace Aw.Sindri.Core.Workflow;

/// <summary>Lift a park or escalation, optionally granting more budget.</summary>
/// <param name="Extra">Budget granted on top of the recipe's.</param>
public sealed record UnparkCommand(Spend Extra) : WorkflowCommand;
