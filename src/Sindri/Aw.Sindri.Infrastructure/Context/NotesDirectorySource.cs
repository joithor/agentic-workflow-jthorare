using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Infrastructure.Context;

/// <summary>
/// The notes source: Markdown and text files under one directory, ranked by <see cref="LineMatches"/>. Hidden files and
/// folders and every symbolic link are skipped (so the walk never leaves the directory or loops), files over 1 MB are
/// skipped, and the walk stops at <see cref="MaxFiles"/> files; it checks the deadline per file. A hit's document is
/// the file's full path, so a person can open it.
/// </summary>
internal sealed class NotesDirectorySource(string directory) : IContextSource
{
    internal const int MaxFiles = 5000;
    private const long MaxBytes = 1024 * 1024;
    private static readonly string[] _extensions = [".md", ".markdown", ".txt"];

    public SourceKind Kind => SourceKind.Notes;

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();   // the walk runs off the caller's thread, under the query's deadline
            var matches = new LineMatches(query.Terms);
            foreach (var file in Files(cancellationToken))
            {
                await Read(file, matches, cancellationToken).ConfigureAwait(false);
            }

            return Results.Success<IReadOnlyList<SourceHit>>(
                [.. matches.Ranked(query.Limit).Select(m => new SourceHit(m.Document, m.Line, Path.GetFileName(m.Document), Snippets.Raw(m.Text)))]);
        }
        catch (OperationCanceledException)
        {
            return SindriErrors.ContextSourceFailed.With("the notes search was cancelled");
        }
        catch (IOException e)
        {
            return SindriErrors.ContextSourceFailed.With($"can't read the notes directory {directory}: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            return SindriErrors.ContextSourceFailed.With($"can't read the notes directory {directory}: {e.Message}");
        }
    }

    private static async Task Read(string file, LineMatches matches, CancellationToken cancellationToken)
    {
        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return;   // removed while the walk ran
        }

        for (var i = 0; i < lines.Length; i++)
        {
            matches.Add(file, i + 1, lines[i]);
        }
    }

    private IEnumerable<string> Files(CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsHidden(file) && _extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) && new FileInfo(file).Length <= MaxBytes)
            {
                found.Add(file);
                if (found.Count == MaxFiles)
                {
                    break;
                }
            }
        }

        return found.Order(StringComparer.Ordinal);
    }

    private bool IsHidden(string file) =>
        Path.GetRelativePath(directory, file).Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.'));
}
