using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>
/// The Step and evidence declarations the first two recipes name (factory addendum §17.28.6 step 1). The work runs outside
/// the engine (a job, a session, a skill) and reports these outputs through a signal.
/// </summary>
public static class BuiltinSteps
{
    /// <summary>The triage job: sizes the item and rates its ambiguity (design spec §7.1).</summary>
    public static IBlock Triage { get; } = Step("step.triage", "Triage job: size and ambiguity of the item",
        outputs: [new("triage.size", PortType.Size), new("triage.ambiguity", PortType.Text)]);

    /// <summary>The claim: creates the claim ref sindri/&lt;user&gt;/&lt;item&gt; (design spec §9.4).</summary>
    public static IBlock Claim { get; } = Step("step.claim", "Creates the item's claim ref", outputs: [new("claim.ref", PortType.Text)]);

    /// <summary>Produce: the session or job that does the Step's work. It runs with tools, so it is capability-bearing.</summary>
    public static IBlock Produce { get; } = Step("step.produce", "The session or job that produces the Step's output", outputs: [], capability: true);

    /// <summary>The Step's deterministic checks (design spec §6).</summary>
    public static IBlock Checks { get; } = Step("step.checks", "The Step's deterministic checks", outputs: [new("checks.verdict", PortType.Verdict)]);

    /// <summary>The Step's verifier: a judge question; the producer never verifies itself (design spec §6).</summary>
    public static IBlock Verify { get; } = Step("step.verify", "The Step's verifier", outputs: [new("verifier.verdict", PortType.Verdict)]);

    /// <summary>The test evidence kind: a recorded test run bound to a sha (§17.11).</summary>
    public static IBlock TestEvidence { get; } = new DeclaredBlock(new BlockSpec(BlockId.From("evidence.test"), BlockKind.Evidence,
        "A recorded test run, bound to the sha it ran on", [], []));

    /// <summary>Every built-in declaration.</summary>
    public static IReadOnlyList<IBlock> All { get; } = [Triage, Claim, Produce, Checks, Verify, TestEvidence];

    private static DeclaredBlock Step(string id, string summary, IReadOnlyList<PortSpec> outputs, bool capability = false) =>
        new(new BlockSpec(BlockId.From(id), BlockKind.Step, summary, [], outputs, capability));
}
