using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>The fact gates the first two recipes name: the router table (design spec §7.1), the claim (§9.4) and the Step contract's checks and verifier (§6).</summary>
public static class BuiltinGates
{
    private static readonly HashSet<string> _startable = new(StringComparer.Ordinal) { "XS", "S", "M" };
    private static readonly PortSpec _size = new("triage.size", PortType.Size);
    private static readonly PortSpec _ambiguity = new("triage.ambiguity", PortType.Text);

    /// <summary>Triage produced a size and an ambiguity.</summary>
    public static IGateBlock TriageComplete { get; } = Gate("gate.triage.complete", "Triage produced a size and an ambiguity", [_size, _ambiguity],
        static _ => new GateVerdict(Passed: true, "triage complete"));

    /// <summary>The router table: an item starts when its ambiguity is 'none' and its size is at most M.</summary>
    public static IGateBlock RouterStartable { get; } = Gate("gate.router.startable", "Router: ambiguity none and size at most M", [_size, _ambiguity],
        static facts => _startable.Contains(facts["triage.size"]) && string.Equals(facts["triage.ambiguity"], "none", StringComparison.Ordinal)
            ? new GateVerdict(Passed: true, "startable")
            : new GateVerdict(Passed: false, $"size {facts["triage.size"]}, ambiguity {facts["triage.ambiguity"]}: needs ambiguity none and size at most M (scope it first)"));

    /// <summary>The claim ref exists and is a sindri/ ref.</summary>
    public static IGateBlock ClaimRef { get; } = Gate("gate.claim.ref", "The claim ref is a sindri/ ref", [new("claim.ref", PortType.Text)],
        static facts => facts["claim.ref"].StartsWith("sindri/", StringComparison.Ordinal)
            ? new GateVerdict(Passed: true, facts["claim.ref"])
            : new GateVerdict(Passed: false, $"'{facts["claim.ref"]}' is not a sindri/<user>/<item> ref"));

    /// <summary>The Step's deterministic checks passed.</summary>
    public static IGateBlock ChecksPassed { get; } = Gate("gate.checks.passed", "The Step's checks passed", [new("checks.verdict", PortType.Verdict)],
        static facts => Verdict(facts["checks.verdict"], "checks"));

    /// <summary>The Step's verifier accepted the output (a judge question, rung 2).</summary>
    public static IGateBlock VerifierPassed { get; } = Gate("gate.verifier.passed", "The verifier accepted the output", [new("verifier.verdict", PortType.Verdict)],
        static facts => Verdict(facts["verifier.verdict"], "verifier"), GateRung.Judge);

    /// <summary>Every built-in gate.</summary>
    public static IReadOnlyList<IGateBlock> All { get; } = [TriageComplete, RouterStartable, ClaimRef, ChecksPassed, VerifierPassed];

    private static GateVerdict Verdict(string verdict, string who) =>
        new(verdict == "pass", $"{who}: {verdict}");

    private static FactGate Gate(string id, string summary, IReadOnlyList<PortSpec> inputs, Func<IReadOnlyDictionary<string, string>, GateVerdict> rule,
        GateRung rung = GateRung.Deterministic) =>
        new(new BlockSpec(BlockId.From(id), BlockKind.Gate, summary, inputs, [], Rung: rung), rule);
}
