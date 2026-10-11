using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Infrastructure.Context;

/// <summary>
/// The code source until the C# code index block exists (docs/csharp/sindri-context.md, "Code index gap"): git grep over
/// each profile repo's tracked files (a repo that fails is left out of the answer; the source fails only when every
/// repo did) (fixed strings, any term, ignoring case, binary files skipped, at most 20 lines a
/// file and <see cref="MaxLines"/> lines a repo), ranked by <see cref="LineMatches"/>. A hit's document is
/// 'repo/path'. Terms are passed with -e and paths after --, so no term is read as an option.
/// </summary>
internal sealed class GitGrepCodeSource(IReadOnlyList<CodeRepo> repos) : IContextSource
{
    internal const int MaxLines = 20_000;

    public SourceKind Kind => SourceKind.Code;

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        var matches = new LineMatches(query.Terms);
        var failures = new List<Error>();
        foreach (var repo in repos)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SindriErrors.ContextSourceFailed.With("the code search was cancelled");
            }

            var grep = await Grep(repo, query.Terms, matches, cancellationToken).ConfigureAwait(false);
            if (grep.IsError)
            {
                failures.Add(grep.Error);
            }
        }

        // One repo that isn't a checkout doesn't blank the others: the source fails only when every repo failed.
        if (failures.Count == repos.Count)
        {
            return SindriErrors.ContextSourceFailed.With(string.Join("; ", failures.Select(f => f.Message)));
        }

        return Results.Success<IReadOnlyList<SourceHit>>(
            [.. matches.Ranked(query.Limit).Select(m => new SourceHit(m.Document, m.Line, Path.GetFileName(m.Document), Snippets.Raw(m.Text)))]);
    }

    private static async Task<Result<Unit>> Grep(CodeRepo repo, IReadOnlyList<string> terms, LineMatches matches, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(Command(repo.Path, terms))
                ?? throw new InvalidOperationException("git didn't start");
            await using var kill = cancellationToken.Register(() => Kill(process)).ConfigureAwait(false);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            var read = await Read(process.StandardOutput, repo.Name, matches, cancellationToken).ConfigureAwait(false);
            if (read >= MaxLines)
            {
                Kill(process);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await errors.ConfigureAwait(false);
            return process.ExitCode is 0 or 1 || read >= MaxLines
                ? Results.Ok()
                : SindriErrors.ContextSourceFailed.With(string.Create(CultureInfo.InvariantCulture,
                    $"git grep in {repo.Path} exited {process.ExitCode}: {stderr.Split('\n')[0].Trim()}"));
        }
        catch (OperationCanceledException)
        {
            return SindriErrors.ContextSourceFailed.With("the code search was cancelled");
        }
        catch (Win32Exception e)
        {
            return SindriErrors.ContextSourceFailed.With($"can't run git (fix: install git and put it on PATH): {e.Message}");
        }
    }

    private static ProcessStartInfo Command(string repoPath, IReadOnlyList<string> terms)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // An inherited GIT_DIR, GIT_WORK_TREE or GIT_INDEX_FILE (sindri started from a hook) would override -C.
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        // Pinned against user config: tracked files only (no fallback to untracked ones), no column field in the output.
        string[] fixedArguments =
        [
            "-C", repoPath, "-c", "grep.fallbackToNoIndex=false", "grep", "-n", "--no-column", "-I", "-i", "-F", "-z", "--no-color", "--max-count=20",
        ];
        foreach (var argument in fixedArguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var term in terms)
        {
            start.ArgumentList.Add("-e");
            start.ArgumentList.Add(term);
        }

        start.ArgumentList.Add("--");
        return start;
    }

    private static async Task<int> Read(StreamReader output, string repoName, LineMatches matches, CancellationToken cancellationToken)
    {
        var count = 0;
        while (count < MaxLines && await output.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var parts = line.Split('\0', 3);
            if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                matches.Add($"{repoName}/{parts[0]}", number, parts[2]);
                count++;
            }
        }

        return count;
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited.
        }
        catch (Win32Exception)
        {
            // It couldn't be signalled (it is exiting); the read loop ends with its output.
        }
    }
}
