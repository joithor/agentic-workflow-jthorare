using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>judge brief-scope: is this brief ready to hand to an agent?</summary>
/// <param name="AgentType">The agent type the brief is for.</param>
/// <param name="Goal">The goal.</param>
/// <param name="AcceptanceCriteria">The acceptance criteria.</param>
/// <param name="ProofCommand">The command that proves it.</param>
public sealed record BriefScopeInput(string AgentType, string Goal, string AcceptanceCriteria, string ProofCommand) : JudgeInput
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Question => "brief-scope";
}
