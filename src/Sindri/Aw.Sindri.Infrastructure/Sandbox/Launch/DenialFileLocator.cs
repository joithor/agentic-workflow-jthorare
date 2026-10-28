using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch.Usage;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>Finds what the denial collector reads for a launch from its plan: the agent's newest transcript under the login's config directory, and the per-launch fail-open log.</summary>
internal static class DenialFileLocator
{
    private const string FailOpenName = "failopen.log";

    /// <summary>Locates the files; a file that is not there yet is null.</summary>
    /// <param name="plan">The launch's plan.</param>
    /// <returns>The transcript's path and kind, and the fail-open log's path.</returns>
    internal static (string? Transcript, TranscriptKind Kind, string? FailOpenLog) For(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var codex = Variable(plan, "CODEX_HOME");
        var kind = codex is null ? TranscriptKind.Claude : TranscriptKind.Codex;
        var root = codex is null ? Variable(plan, "CLAUDE_CONFIG_DIR") is { } claude ? Path.Combine(claude, "projects") : null : Path.Combine(codex, "sessions");
        return (root is null ? null : Newest(root), kind, FailOpenLog(plan));
    }

    private static string? Variable(SandboxPlan plan, string name) =>
        plan.Environment.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal))?.Value;

    private static string? FailOpenLog(SandboxPlan plan)
    {
        var logs = plan.Mounts.FirstOrDefault(m => m.Kind == PlanMountKind.PerLaunchDir && m.Path.EndsWith("/logs", StringComparison.Ordinal))?.Source;
        return logs is null ? null : Path.Combine(logs, FailOpenName);
    }

    private static string? Newest(string root) =>
        TranscriptFiles.Jsonl(root, since: null).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
}
