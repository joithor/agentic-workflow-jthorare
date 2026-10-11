namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>
/// The judge's decision questions that gate work (docs/csharp/judge.md, Questions). wake-gate (routing a message) and
/// ui-element-repair (choosing a selector inside ui-evidence) answer no pass-or-fail question about work, so they aren't gates.
/// Fact names are '&lt;topic&gt;.&lt;field&gt;'; a flag fact is 'true' or 'false'.
/// </summary>
internal static class JudgeQuestions
{
    private static readonly PortSpec _brief = Text("resolution.brief");
    private static readonly PortSpec _expected = Text("resolution.expected");
    private static readonly PortSpec _actual = Text("resolution.actual");
    private static readonly PortSpec _rootCause = Text("resolution.root-cause");
    private static readonly PortSpec _checkKind = Text("resolution.check-kind");
    private static readonly PortSpec _checkSummary = Text("resolution.check-summary");
    private static readonly PortSpec _beforePassed = new("resolution.before-passed", PortType.Flag);
    private static readonly PortSpec _afterPassed = new("resolution.after-passed", PortType.Flag);
    private static readonly PortSpec _diffStat = Text("resolution.diff-stat");

    /// <summary>resolution-check: passes when the problem is resolved.</summary>
    internal static JudgeQuestion ResolutionCheck { get; } = new("resolution-check", "Judge: the reported problem is resolved by the change",
        [_brief, _expected, _actual, _rootCause, _checkKind, _checkSummary, _beforePassed, _afterPassed, _diffStat], ["resolved"],
        static f => new ResolutionCheckInput(f[_brief.Name], f[_expected.Name], f[_actual.Name], f[_rootCause.Name], f[_checkKind.Name],
            f[_checkSummary.Name], Flag(f[_beforePassed.Name]), Flag(f[_afterPassed.Name]), f[_diffStat.Name], Optional(f, "resolution.diff")));

    /// <summary>rule-check: passes when the hunk is fine or the rule doesn't apply.</summary>
    internal static JudgeQuestion RuleCheck { get; } = new("rule-check", "Judge: the hunk doesn't violate the rule",
        [Text("rule.text"), Text("rule.hunk")], ["fine", "n/a"],
        static f => new RuleCheckInput(f["rule.text"], f["rule.hunk"]));

    /// <summary>turn-progress: passes when the turn is progressing.</summary>
    internal static JudgeQuestion TurnProgress { get; } = new("turn-progress", "Judge: the turn moves the problem forward",
        [Text("progress.problem"), Text("progress.turn-diff")], ["progressing"],
        static f => new TurnProgressInput(f["progress.problem"], f["progress.turn-diff"]));

    /// <summary>brief-scope: passes when the brief is ready.</summary>
    internal static JudgeQuestion BriefScope { get; } = new("brief-scope", "Judge: the brief is ready to hand to an agent",
        [Text("brief.agent-type"), Text("brief.goal"), Text("brief.acceptance-criteria"), Text("brief.proof-command")], ["ready"],
        static f => new BriefScopeInput(f["brief.agent-type"], f["brief.goal"], f["brief.acceptance-criteria"], f["brief.proof-command"]));

    /// <summary>visual-critique: passes when the screenshot looks right.</summary>
    internal static JudgeQuestion VisualCritique { get; } = new("visual-critique", "Judge: the UI screenshot looks right",
        [Text("visual.evidence-dir"), Text("visual.screenshot")], ["looks-right"],
        static f => new VisualCritiqueInput(f["visual.screenshot"], f["visual.evidence-dir"], Optional(f, "visual.baseline")));

    /// <summary>ask-check: passes when the agent may continue without asking.</summary>
    internal static JudgeQuestion AskCheck { get; } = new("ask-check", "Judge: the agent may continue without asking the human",
        [Text("ask.transcript-tail")], ["continue"],
        static f => new AskCheckInput(f["ask.transcript-tail"]));

    /// <summary>Every gating question.</summary>
    internal static IReadOnlyList<JudgeQuestion> All { get; } = [ResolutionCheck, RuleCheck, TurnProgress, BriefScope, VisualCritique, AskCheck];

    private static PortSpec Text(string name) => new(name, PortType.Text);

    private static bool Flag(string value) => string.Equals(value, "true", StringComparison.Ordinal);

    private static string? Optional(IReadOnlyDictionary<string, string> facts, string name) =>
        facts.TryGetValue(name, out var value) && value.Trim().Length > 0 ? value : null;
}
