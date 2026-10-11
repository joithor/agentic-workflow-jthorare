using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>
/// The judge's envelope for one question: decided (<see cref="Decision"/>, <see cref="Confidence"/>, <see cref="Model"/>,
/// <see cref="Id"/>) or escalated (<see cref="Escalated"/> true, no decision). Other fields the judge sends are ignored.
/// </summary>
/// <param name="ReasonCode">Why it decided or escalated.</param>
/// <param name="Decision">The decision, when decided.</param>
/// <param name="Confidence">The deciding provider's confidence, 0 to 1.</param>
/// <param name="Model">The provider that decided ('rules' for a pre-rule).</param>
/// <param name="Id">The decision id, for 'judge why'.</param>
/// <param name="Escalated">True when the judge couldn't decide.</param>
public sealed record JudgeAnswer(string ReasonCode, string? Decision = null, double? Confidence = null, string? Model = null, JudgeDecisionId? Id = null, bool Escalated = false);
