namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>A finished tool process.</summary>
/// <param name="Exit">Its exit code.</param>
/// <param name="Stdout">Its standard output, as bytes (one JSON document for these tools).</param>
/// <param name="Stderr">Its standard error.</param>
internal sealed record ToolRun(int Exit, byte[] Stdout, string Stderr)
{
    /// <summary>The first stderr line that isn't a JSON log line, for an error message.</summary>
    public string Message => Stderr.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('{')) ?? "no message on stderr";
}
