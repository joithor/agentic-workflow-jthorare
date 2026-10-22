namespace Aw.Sindri.Host.Commands.Scrub;

/// <summary>Which binary the pre-commit hook calls.</summary>
internal static class ScrubBinary
{
    /// <summary>The name of the environment variable that overrides it.</summary>
    internal const string Variable = "SINDRI_BIN";

    /// <summary>
    /// SINDRI_BIN when set; else the running binary when it is the published 'sindri' (the hook calls it by absolute path, since
    /// GUI git clients don't load ~/.local/bin into PATH); else 'sindri' from PATH (a 'dotnet run' or 'dotnet sindri.dll' run).
    /// </summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="processPath">The running process's path, if known.</param>
    /// <returns>The path or name.</returns>
    internal static string Path(Func<string, string?> environment, string? processPath)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment(Variable) is { Length: > 0 } configured)
        {
            return configured;
        }

        return processPath is { Length: > 0 } && string.Equals(System.IO.Path.GetFileNameWithoutExtension(processPath), "sindri", StringComparison.Ordinal) ? processPath : "sindri";
    }
}
