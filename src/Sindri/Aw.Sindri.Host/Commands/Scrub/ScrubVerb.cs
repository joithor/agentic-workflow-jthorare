using System.Text;
using Aw.Kernel;
using Aw.Redaction;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands.Scrub;

/// <summary>One hit of a scrubbed text, as --json prints it: the rule and the span, never the matched value.</summary>
/// <param name="Rule">The rule id.</param>
/// <param name="Start">The first redacted UTF-16 index.</param>
/// <param name="End">The index after the last redacted one.</param>
internal sealed record ScrubHitJson(string Rule, int Start, int End);

/// <summary>'sindri scrub --json' over standard input.</summary>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Hits">The hits, in input order.</param>
internal sealed record ScrubTextJson(string Text, IReadOnlyList<ScrubHitJson> Hits);

/// <summary>One likely secret in staged changes: where it is and which rule named it.</summary>
/// <param name="File">The file.</param>
/// <param name="Line">The line in the staged file where the secret starts.</param>
/// <param name="Rule">The rule id.</param>
internal sealed record StagedHitJson(string File, int Line, string Rule);

/// <summary>'sindri scrub --staged --json'.</summary>
/// <param name="Ok">True when nothing was found.</param>
/// <param name="Hits">The likely secrets.</param>
internal sealed record ScrubStagedJson(bool Ok, IReadOnlyList<StagedHitJson> Hits);

/// <summary>'sindri scrub --install-pre-commit --json'.</summary>
/// <param name="Hook">The hook's path.</param>
/// <param name="Changed">False when the same hook was already there.</param>
internal sealed record ScrubInstallJson(string Hook, bool Changed);

/// <summary>
/// 'sindri scrub [--json]', 'sindri scrub --staged [--json]' and 'sindri scrub --install-pre-commit [--repo PATH] [--json]'. The
/// scrubber and the pre-commit gate over Aw.Redaction's built-in rules. It reads standard input and git, writes nothing but
/// the hook, and never starts or needs the control plane, so a pre-commit hook can call it. Exit 0: clean (or text scrubbed
/// and passed on); 1: staged secrets found or the scan withheld the text; 2: bad usage or an error (a fail-closed hook blocks).
/// </summary>
/// <param name="io">Standard input, the output and the working directory.</param>
/// <param name="git">Runs git.</param>
/// <param name="redactor">The rules.</param>
/// <param name="binary">The binary a hook calls when it runs.</param>
internal sealed class ScrubVerb(ScrubIo io, GitRun git, IRedactor redactor, string binary)
{
    private const string Usage = "scrub [--json] | scrub --staged [--json] | scrub --install-pre-commit [--repo <path>] [--json]";

    // -M and --diff-filter=d: renamed, copied and type-changed files are scanned too. --text: a binary-looking file or a
    // '-diff' attribute would otherwise print "Binary files differ" with no lines and hide a secret from the scan.
    private static readonly string[] _stagedDiff =
        ["-c", "core.quotePath=false", "diff", "--cached", "--unified=0", "--no-color", "--no-ext-diff", "--text", "--no-textconv", "-M", "--diff-filter=d"];

    internal async Task<int> Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (Parse(args) is not { } options)
        {
            return await io.Output.Error(SindriErrors.InvalidInput.With($"'scrub {string.Join(' ', args)}' is not a scrub command: {Usage}")).ConfigureAwait(false);
        }

        if (options.Install)
        {
            return await Install(options.Repo, options.Json).ConfigureAwait(false);
        }

        return options.Staged ? await Staged(options.Json).ConfigureAwait(false) : await Text(options.Json).ConfigureAwait(false);
    }

    private sealed record Options(bool Json, bool Staged, bool Install, string? Repo);

    // Null when the arguments are not one of the three forms: an unknown word, --repo without a path or without --install-pre-commit, --staged with --install-pre-commit.
    private static Options? Parse(string[] args)
    {
        bool json = false, staged = false, install = false;
        string? repo = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--staged":
                    staged = true;
                    break;
                case "--install-pre-commit":
                    install = true;
                    break;
                case "--repo" when i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    repo = args[++i];
                    break;
                default:
                    return null;
            }
        }

        return (staged && install) || (repo is not null && !install) ? null : new Options(json, staged, install, repo);
    }

    private async Task<int> Text(bool json)
    {
        var result = redactor.Scrub(await io.Stdin.ReadToEndAsync().ConfigureAwait(false));
        if (result.IsBlocked)
        {
            await io.Stderr.WriteLineAsync($"{ScrubErrors.Blocked.Code} {ScrubErrors.Blocked.Title}; nothing was written").ConfigureAwait(false);
            return 1;
        }

        if (result.Hits.Count > 0)
        {
            await io.Stderr.WriteLineAsync($"scrubbed {result.Hits.Count} hit(s)").ConfigureAwait(false);
        }

        // A filter: the text passes through as it is; --json wraps it with the hits.
        return json
            ? await io.Output.Json(new ScrubTextJson(result.Text, [.. result.Hits.Select(static h => new ScrubHitJson(h.Rule, h.Start, h.End))]), 0).ConfigureAwait(false)
            : await io.Output.Text(result.Text, 0).ConfigureAwait(false);
    }

    private async Task<int> Staged(bool json)
    {
        var diff = git(io.WorkingDirectory, _stagedDiff);
        if (!diff.Ok)
        {
            // Outside a repo, git diff falls back to --no-index and its error says nothing useful.
            return git(io.WorkingDirectory, ["rev-parse", "--git-dir"]).Ok
                ? await Fail(ScrubErrors.GitFailed.With($"git diff --cached failed, so the staged changes were not scanned: {FirstLine(diff.Stderr)}")).ConfigureAwait(false)
                : await Fail(ScrubErrors.NotARepo.With($"{io.WorkingDirectory} is not inside a git repo")).ConfigureAwait(false);
        }

        var hits = new List<StagedHitJson>();
        foreach (var file in StagedDiff.Parse(diff.Stdout))
        {
            var found = Find(file);
            if (found is null)
            {
                return await Fail(ScrubErrors.Blocked.With($"{file.File} could not be scanned in time, so the commit is refused")).ConfigureAwait(false);
            }

            hits.AddRange(found);
        }

        if (hits.Count == 0)
        {
            return json ? await io.Output.Json(new ScrubStagedJson(Ok: true, []), 0).ConfigureAwait(false) : await io.Output.Text("No secrets in staged changes.\n", 0).ConfigureAwait(false);
        }

        if (json)
        {
            return await io.Output.Json(new ScrubStagedJson(Ok: false, hits), 1).ConfigureAwait(false);
        }

        var text = new StringBuilder($"{ScrubErrors.StagedSecrets.Code} refused: {hits.Count} likely secret(s) in staged changes:\n");
        foreach (var hit in hits)
        {
            _ = text.Append("  ").Append(hit.File).Append(':').Append(hit.Line).Append(' ').Append(hit.Rule).Append('\n');
        }

        _ = text.Append("  fix: remove them (use a secret pointer or an env var); for a false positive, commit with --no-verify and say why\n");
        await io.Stderr.WriteAsync(text.ToString()).ConfigureAwait(false);
        return 1;
    }

    // One file's additions are scanned as one text, so a secret split over lines (a PEM private key) is still found; each hit maps back to its first line. Null: the scan was withheld.
    private List<StagedHitJson>? Find(AddedFile file)
    {
        var starts = new List<int>(file.Lines.Count);
        var offset = 0;
        foreach (var line in file.Lines)
        {
            starts.Add(offset);
            offset += line.Text.Length + 1;
        }

        var result = redactor.Scrub(string.Join('\n', file.Lines.Select(static l => l.Text)));
        if (result.IsBlocked)
        {
            return null;
        }

        return [.. result.Hits.Select(h =>
        {
            var i = starts.Count - 1;
            while (starts[i] > h.Start)
            {
                i--;
            }

            return new StagedHitJson(file.File, file.Lines[i].Number, h.Rule);
        })];
    }

    private async Task<int> Fail(Error error) => await io.Output.Error(error).ConfigureAwait(false);

    private async Task<int> Install(string? repo, bool json)
    {
        var directory = Path.GetFullPath(repo ?? ".", io.WorkingDirectory);
        var path = git(directory, ["rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit"]);
        if (!path.Ok)
        {
            return await Fail(ScrubErrors.NotARepo.With($"{directory} is not inside a git repo")).ConfigureAwait(false);
        }

        var hook = path.Stdout.Trim();
        var text = PreCommitHook.Text(binary);
        var old = File.Exists(hook) ? await File.ReadAllTextAsync(hook).ConfigureAwait(false) : null;
        // A hooks dir core.hooksPath names (local or global) is the user's: often tracked, maybe public. Never write there, not even into an empty one, and never set core.hooksPath.
        if (git(directory, ["config", "--get", "core.hooksPath"]) is { Ok: true } hooksPath)
        {
            return await Fail(ScrubErrors.ForeignHook.With(
                $"core.hooksPath ({hooksPath.Stdout.Trim()}) points the pre-commit hook at {hook}, in a directory sindri didn't create; nothing was written (fix: add this line to that pre-commit hook yourself: '{binary.Replace("'", "'\\''", StringComparison.Ordinal)}' scrub --staged || exit 1)")).ConfigureAwait(false);
        }

        if (old is not null && !PreCommitHook.IsSindri(old))
        {
            return await Fail(ScrubErrors.ForeignHook.With($"{hook} already exists and is not sindri's; nothing was written (fix: add '{binary.Replace("'", "'\\''", StringComparison.Ordinal)}' scrub --staged || exit 1 to it yourself)")).ConfigureAwait(false);
        }

        var changed = !string.Equals(old, text, StringComparison.Ordinal);
        if (changed)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
            await File.WriteAllTextAsync(hook, text).ConfigureAwait(false);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return json
            ? await io.Output.Json(new ScrubInstallJson(hook, changed), 0).ConfigureAwait(false)
            : await io.Output.Text(changed ? $"Installed the secret-scan pre-commit hook at {hook}.\n" : $"The secret-scan pre-commit hook at {hook} is already installed.\n", 0).ConfigureAwait(false);
    }

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();
}

/// <summary>What the scrub verb reads and writes.</summary>
/// <param name="Stdin">Standard input.</param>
/// <param name="Output">Standard output and the error printer.</param>
/// <param name="Stderr">Standard error.</param>
/// <param name="WorkingDirectory">The directory the verb ran in.</param>
internal sealed record ScrubIo(TextReader Stdin, Output Output, TextWriter Stderr, string WorkingDirectory);
