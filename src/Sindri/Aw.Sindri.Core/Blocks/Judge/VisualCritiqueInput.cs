using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>judge visual-critique: does the screenshot look right?</summary>
/// <param name="AfterScreenshot">The screenshot, relative to <paramref name="EvidenceDir"/>.</param>
/// <param name="EvidenceDir">The absolute evidence directory.</param>
/// <param name="BaselineScreenshot">A baseline screenshot relative to it, when there is one.</param>
public sealed record VisualCritiqueInput(
    string AfterScreenshot,
    string EvidenceDir,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BaselineScreenshot = null) : JudgeInput
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Question => "visual-critique";
}
