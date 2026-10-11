namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>
/// Where the tool blocks find their binaries: AW_SINDRI_JUDGE_BIN (default 'judge' on PATH) and AW_SINDRI_UI_EVIDENCE_BIN
/// (default 'ui-evidence' on PATH). Before the switch PATH's 'judge' is the TypeScript one, so a run of the C# blocks points
/// AW_SINDRI_JUDGE_BIN at the C# judge (docs/csharp/sindri-blocks.md).
/// </summary>
/// <param name="JudgeBin">The judge binary.</param>
/// <param name="UiEvidenceBin">The ui-evidence binary.</param>
internal sealed record ToolSettings(string JudgeBin, string UiEvidenceBin)
{
    /// <summary>How long one judge question may take: its own budget is at most 20 s, plus start-up.</summary>
    public static TimeSpan JudgeTimeout { get; } = TimeSpan.FromSeconds(60);

    /// <summary>How long doctor and provenance may take.</summary>
    public static TimeSpan ProbeTimeout { get; } = TimeSpan.FromSeconds(60);

    /// <summary>How long a run or parity measurement may take, its two lock waits (120 s each) included.</summary>
    public static TimeSpan RunTimeout { get; } = TimeSpan.FromMinutes(20);

    /// <summary>Reads the settings.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The settings.</returns>
    public static ToolSettings Read(Func<string, string?> environment) =>
        new(environment("AW_SINDRI_JUDGE_BIN") is { Length: > 0 } judge ? judge : "judge",
            environment("AW_SINDRI_UI_EVIDENCE_BIN") is { Length: > 0 } ui ? ui : "ui-evidence");
}
