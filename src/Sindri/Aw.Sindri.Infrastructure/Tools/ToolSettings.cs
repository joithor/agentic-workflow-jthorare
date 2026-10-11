namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>
/// Where the tool blocks find their binaries: AW_SINDRI_JUDGE_BIN (default 'judge' on PATH) and AW_SINDRI_UI_EVIDENCE_BIN
/// (default 'ui-evidence' on PATH), read from the control plane's environment when it starts. Before the switch PATH's
/// 'judge' is the TypeScript one, so a run of the C# blocks points AW_SINDRI_JUDGE_BIN at the C# judge
/// (docs/csharp/sindri-blocks.md). The timeouts are Sindri's bounds on one tool call.
/// </summary>
/// <param name="JudgeBin">The judge binary.</param>
/// <param name="UiEvidenceBin">The ui-evidence binary.</param>
internal sealed record ToolSettings(string JudgeBin, string UiEvidenceBin)
{
    private const string DefaultJudgeBin = "judge";
    private const string DefaultUiEvidenceBin = "ui-evidence";

    /// <summary>
    /// One judge question. judge's budget is per provider call (up to 20 s) and its chain tries up to four providers in
    /// turn (docs/csharp/judge.md, Providers), so a whole chain can take about 80 s before it escalates on its own.
    /// </summary>
    public TimeSpan JudgeTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>doctor and provenance (each of ui-evidence's probes is bounded at 15 s).</summary>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>A run or parity measurement, its two lock waits (120 s each) included.</summary>
    public TimeSpan RunTimeout { get; init; } = TimeSpan.FromMinutes(20);

    /// <summary>How long a stopped tool gets to exit after SIGTERM (ui-evidence releases its machine locks then) before SIGKILL.</summary>
    public TimeSpan StopGrace { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Reads the settings.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The settings.</returns>
    public static ToolSettings Read(Func<string, string?> environment) =>
        new(environment("AW_SINDRI_JUDGE_BIN") is { Length: > 0 } judge ? judge : DefaultJudgeBin,
            environment("AW_SINDRI_UI_EVIDENCE_BIN") is { Length: > 0 } ui ? ui : DefaultUiEvidenceBin);
}
