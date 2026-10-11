using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>judge resolution-check: is the reported problem solved by this change?</summary>
/// <param name="Brief">The problem as reported.</param>
/// <param name="Expected">What should happen.</param>
/// <param name="Actual">What happened before the fix.</param>
/// <param name="RootCause">The root cause found.</param>
/// <param name="CheckKind">'ui-evidence' or 'test'.</param>
/// <param name="CheckSummary">What the check does.</param>
/// <param name="BeforePassed">Whether the check passed before the fix.</param>
/// <param name="AfterPassed">Whether it passes after.</param>
/// <param name="DiffStat">The change's diff stat.</param>
/// <param name="Diff">The diff, when given.</param>
public sealed record ResolutionCheckInput(
    string Brief,
    string Expected,
    string Actual,
    string RootCause,
    string CheckKind,
    string CheckSummary,
    bool BeforePassed,
    bool AfterPassed,
    string DiffStat,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Diff = null) : JudgeInput
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Question => "resolution-check";
}
