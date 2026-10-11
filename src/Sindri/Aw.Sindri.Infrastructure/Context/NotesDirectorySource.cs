using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Infrastructure.Context;

/// <summary>
/// The notes source: Markdown and text files under one directory (hidden files and folders skipped, at most
/// <see cref="MaxFiles"/> files of up to 1 MB each), ranked by <see cref="LineMatches"/>. A hit's document is the file's
/// full path, so a person can open it.
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
            var matches = new LineMatches(query.Terms);
            foreach (var file in Files())
            {
                var lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
                for (var i = 0; i < lines.Length; i++)
                {
                    matches.Add(file, i + 1, lines[i]);
                }
            }

            return Results.Success<IReadOnlyList<SourceHit>>(
                [.. matches.Ranked(query.Limit).Select(m => new SourceHit(m.Document, m.Line, Path.GetFileName(m.Document), Snippets.Line(m.Text)))]);
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

    private IEnumerable<string> Files()
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };
        return Directory.EnumerateFiles(directory, "*", options)
            .Where(f => !IsHidden(f) && _extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) && new FileInfo(f).Length <= MaxBytes)
            .Order(StringComparer.Ordinal)
            .Take(MaxFiles);
    }

    private bool IsHidden(string file) =>
        Path.GetRelativePath(directory, file).Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.'));
}
