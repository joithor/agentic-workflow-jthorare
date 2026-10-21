using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The Step of the 'workflow.pr-babysitter' STUB (plan Q3): one human-only Step that stands for babysitting, docs sync and the owner's
/// merge until the PR babysitter spec's recipe replaces the stub. Descriptor only.
/// </summary>
internal static class BabysitSteps
{
    /// <summary>'step.babysit.watch': the human says 'merged' (with the merged sha), 'rework' (with why it goes back to build) or 'stuck'.</summary>
    public static BlockDescriptor Watch { get; } = HumanDeskStep("step.babysit.watch", Capability.None)
        .Reads("fact:pr", "fact:handoff", "fact:phasePacket", "fact:lineContext", "fact:workspace", "fact:handoffNote")
        .Reports("merged", "rework", "stuck")
        .Writes(On("fact:mergedSha", "merged"), On("fact:reworkNotes", "rework"));

    /// <summary>'step.pr.snapshot' (lite): one read of the pull request and the first outcome that holds (PR babysitter spec §4.3); 'fact:prState' on every outcome, the failing check names as 'fact:findings' when red.</summary>
    public static BlockDescriptor Snapshot { get; } = BlockStep("step.pr.snapshot", Capability.Network)
        .Reads("fact:pr")
        .Reports("merged", "closed", "ciRed", "attention", "pending", "green")
        .Writes(Every("fact:prState"), On("fact:mergedSha", "merged"), On("fact:findings", "ciRed"));

    /// <summary>'step.pr.ci-log': the failing checks' logs, fenced, and the check that the workspace stands on the pull request's head.</summary>
    public static BlockDescriptor CiLog { get; } = BlockStep("step.pr.ci-log", Capability.Network)
        .Reads("fact:pr", "fact:prState", "fact:workspace")
        .Reports("ready", "cannotFix")
        .Writes(On("fact:ciLog", "ready"), On("fact:handoffNote", "cannotFix"));

    /// <summary>'step.pr.ci-fix': an agent fixes the failing required checks in the phase worktree and commits; Sindri pushes (PR babysitter spec A.1).</summary>
    public static BlockDescriptor CiFix { get; } = AgentStep("step.pr.ci-fix", Capability.Tools | Capability.WriteScope)
        .Reads("fact:prState", "fact:ciLog", "fact:handoffNote", "fact:phasePacket", "fact:workspace")
        .Reports("fixed", "flaky", "cannotFix")
        .Writes(Every("fact:handoffNote"));
}
