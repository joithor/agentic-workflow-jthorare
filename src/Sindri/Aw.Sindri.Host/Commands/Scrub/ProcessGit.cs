using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Host.Commands.Scrub;

/// <summary>What one git call answered.</summary>
/// <param name="Ok">True when git ran and exited 0.</param>
/// <param name="Stdout">Standard output.</param>
/// <param name="Stderr">Standard error.</param>
internal sealed record GitAnswer(bool Ok, string Stdout, string Stderr);

/// <summary>Runs git in a directory with an argument list (no shell).</summary>
/// <param name="directory">The working directory.</param>
/// <param name="arguments">The arguments, one per element.</param>
/// <returns>The answer.</returns>
internal delegate GitAnswer GitRun(string directory, IReadOnlyList<string> arguments);

/// <summary>The real git: a child process whose output is read on both pipes at once, so a large diff never fills one.</summary>
internal static class ProcessGit
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(60);

    internal static GitAnswer Run(string directory, IReadOnlyList<string> arguments) => Run(directory, arguments, environment: null);

    /// <summary>Runs git with some environment variables set (a null value removes one); tests use it to keep the user's git config out.</summary>
    internal static GitAnswer Run(string directory, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string?>? environment)
    {
        var start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            if (value is null)
            {
                _ = start.Environment.Remove(name);
            }
            else
            {
                start.Environment[name] = value;
            }
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return new GitAnswer(false, string.Empty, "git did not start");
            }

            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(_budget))
            {
                process.Kill(entireProcessTree: true);
                return new GitAnswer(false, string.Empty, "git timed out");
            }

            process.WaitForExit();
#pragma warning disable RS0030 // the child has exited, so both pipes reach end of file at once
            return new GitAnswer(process.ExitCode == 0, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
#pragma warning restore RS0030
        }
        catch (Win32Exception)
        {
            return new GitAnswer(false, string.Empty, "git was not found on PATH");
        }
    }
}
