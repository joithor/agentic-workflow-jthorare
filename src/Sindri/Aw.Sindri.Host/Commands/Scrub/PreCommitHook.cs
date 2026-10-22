using System.Text;

namespace Aw.Sindri.Host.Commands.Scrub;

/// <summary>
/// The pre-commit hook 'sindri scrub --install-pre-commit' writes. It calls the installed binary by the absolute path it was
/// installed from (GUI git clients don't load ~/.local/bin into PATH) and fails closed when that binary is missing; the scan
/// itself never needs the control plane. 'git commit --no-verify' still skips it: a deliberate, visible choice.
/// </summary>
internal static class PreCommitHook
{
    /// <summary>The line that marks a hook as sindri's.</summary>
    internal const string Marker = "# sindri-pre-commit v3";

    // The markers of the hooks the TypeScript Sindri wrote: this one replaces them.
    private static readonly string[] _earlier = ["# sindri-pre-commit v2", "# sindri-scrub-pre-commit v1"];

    /// <summary>The hook's text.</summary>
    /// <param name="binary">The binary the hook runs: an absolute path, or a name found on PATH.</param>
    /// <returns>The script.</returns>
    internal static string Text(string binary)
    {
        ArgumentException.ThrowIfNullOrEmpty(binary);
        var quoted = binary.Replace("'", "'\\''", StringComparison.Ordinal);
        return new StringBuilder()
            .Append("#!/bin/sh\n")
            .Append(Marker).Append('\n')
            .Append("# Refuses commits that add secret-shaped strings. Installed by `sindri scrub --install-pre-commit`.\n")
            .Append("SINDRI='").Append(quoted).Append("'\n")
            .Append("if [ ! -x \"$SINDRI\" ] && ! command -v \"$SINDRI\" >/dev/null 2>&1; then\n")
            .Append("  echo \"sindri-scrub: $SINDRI not found, so the secret scan can't run; refusing the commit.\" >&2\n")
            .Append("  echo \"  fix: reinstall sindri (or commit with --no-verify and say why)\" >&2\n")
            .Append("  exit 1\n")
            .Append("fi\n")
            .Append("\"$SINDRI\" scrub --staged || exit 1\n")
            .ToString();
    }

    /// <summary>True when a whole line of <paramref name="text"/> is one of sindri's markers (a foreign hook that merely mentions one is not ours).</summary>
    /// <param name="text">An existing hook.</param>
    /// <returns>Whether sindri wrote it.</returns>
    internal static bool IsSindri(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split('\n').Any(static l => l.TrimEnd('\r') is var line && (string.Equals(line, Marker, StringComparison.Ordinal) || _earlier.Contains(line, StringComparer.Ordinal)));
    }
}
