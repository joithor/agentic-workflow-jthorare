using Aw.Sindri.Core.Blocks.Builtin;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>The Step workflow.ui-evidence names: whoever drives the run collects the evidence and reports where its screenshot is.</summary>
internal static class UiEvidenceSteps
{
    /// <summary>'step.ui-run': runs the UI script (sindri workflow collect … ui-browser) and reports the evidence directory and screenshot.</summary>
    internal static IBlock UiRun { get; } = new DeclaredBlock(new BlockSpec(BlockId.From("step.ui-run"), BlockKind.Step,
        "Runs the UI script through the ui-browser provider; reports the evidence directory and the screenshot to judge", [],
        [new("visual.evidence-dir", PortType.Text), new("visual.screenshot", PortType.Text), new("visual.baseline", PortType.Text)]));
}
