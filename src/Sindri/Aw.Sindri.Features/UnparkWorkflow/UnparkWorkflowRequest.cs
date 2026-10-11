using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.UnparkWorkflow;

/// <summary>Lift a park or escalation (the human's answer, §17.28.1); optionally grant more budget.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Tokens">Tokens granted on top of the budget.</param>
/// <param name="Turns">Turns granted on top of the budget.</param>
public sealed record UnparkWorkflowRequest(InstanceId Instance, long Tokens = 0, int Turns = 0);
