using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>judge turn-progress: is this turn moving the problem forward?</summary>
/// <param name="Problem">The problem being worked.</param>
/// <param name="TurnDiff">The turn's diff.</param>
public sealed record TurnProgressInput(string Problem, string TurnDiff) : JudgeInput
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Question => "turn-progress";
}
