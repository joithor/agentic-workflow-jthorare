namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>A finished tool process.</summary>
/// <param name="Exit">Its exit code.</param>
/// <param name="Stdout">Its standard output, as bytes (one JSON document for these tools).</param>
/// <param name="Stderr">Its standard error.</param>
internal sealed record ToolRun(int Exit, byte[] Stdout, string Stderr)
{
    /// <summary>
    /// The last stderr line that isn't a JSON log line, for an error message: both tools end with their error line, after
    /// progress lines such as ui-evidence's 'waiting on lock'.
    /// </summary>
    public string Message => Stderr.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0 && !l.StartsWith('{')) ?? "no message on stderr";
}
