using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>judge rule-check: does this hunk violate the rule?</summary>
/// <param name="Rule">The rule's text.</param>
/// <param name="Hunk">The diff hunk.</param>
public sealed record RuleCheckInput(string Rule, string Hunk) : JudgeInput
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Question => "rule-check";
}
