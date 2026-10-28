using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>Finds what the denial collector reads for a launch from its plan: the launch's own transcript under the login's config directory, and the per-launch fail-open log.</summary>
internal static class DenialFileLocator
{
    private const string FailOpenName = "failopen.log";

    /// <summary>Locates the files; a file that is not there (or a session that was never reported) is null.</summary>
    /// <param name="plan">The launch's plan.</param>
    /// <param name="glob">The catalog entry's transcript glob with '{sessionId}', or null.</param>
    /// <param name="session">The launch's own session id, or null.</param>
    /// <returns>The transcript's path and kind, and the fail-open log's path.</returns>
    internal static (string? Transcript, TranscriptKind Kind, string? FailOpenLog) For(SandboxPlan plan, string? glob, string? session)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var codex = Variable(plan, "CODEX_HOME");
        var claude = Variable(plan, "CLAUDE_CONFIG_DIR");
        var kind = codex is null ? TranscriptKind.Claude : TranscriptKind.Codex;
        return (Transcript(glob, session, claude, codex), kind, FailOpenLog(plan));
    }

    // The agent writes under the config directory the plan gave it, not the real ~/.claude or ~/.codex the catalog's glob names.
    private static string? Transcript(string? glob, string? session, string? claude, string? codex)
    {
        if (glob is null || session is null)
        {
            return null;
        }

        var pattern = glob.Replace("{sessionId}", session, StringComparison.Ordinal);
        pattern = Rebase(pattern, "~/.claude", claude) ?? Rebase(pattern, "~/.codex", codex) ?? string.Empty;
        return pattern.Length == 0 ? null : Sessions.TranscriptFiles.Expand(pattern).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private static string? Rebase(string pattern, string prefix, string? directory) =>
        directory is not null && pattern.StartsWith(prefix, StringComparison.Ordinal) ? directory + pattern[prefix.Length..] : null;

    private static string? Variable(SandboxPlan plan, string name) =>
        plan.Environment.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal))?.Value;

    private static string? FailOpenLog(SandboxPlan plan)
    {
        var logs = plan.Mounts.FirstOrDefault(m => m.Kind == PlanMountKind.PerLaunchDir && m.Path.EndsWith("/logs", StringComparison.Ordinal))?.Source;
        return logs is null ? null : Path.Combine(logs, FailOpenName);
    }
}
