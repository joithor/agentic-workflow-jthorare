using System.Globalization;

namespace Aw.Sindri.Host.Commands.Scrub;

/// <summary>One added line of a staged file: its number in the new file and its text.</summary>
/// <param name="Number">The 1-based line number in the staged file.</param>
/// <param name="Text">The line, without the leading '+'.</param>
internal readonly record struct AddedLine(int Number, string Text);

/// <summary>A staged file and the lines the commit adds to it.</summary>
/// <param name="File">The path in the repo.</param>
/// <param name="Lines">The added lines, in order; never empty.</param>
internal sealed record AddedFile(string File, IReadOnlyList<AddedLine> Lines);

/// <summary>Reads the added lines out of 'git diff --cached --unified=0'.</summary>
internal static class StagedDiff
{
    /// <summary>
    /// Parses a unified diff with no context lines. Inside a hunk the header's counts say how many lines are left, so an added
    /// line whose text starts with "++ " is still an addition and never taken for a '+++ ' file header.
    /// </summary>
    /// <param name="diff">The diff text.</param>
    /// <returns>The files that gained at least one line.</returns>
    internal static IReadOnlyList<AddedFile> Parse(string diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var files = new List<(string File, List<AddedLine> Lines)>();
        List<AddedLine>? current = null;
        var number = 0;
        var oldLeft = 0;
        var newLeft = 0;
        foreach (var line in diff.Split('\n'))
        {
            if (oldLeft > 0 || newLeft > 0)
            {
                if (line.StartsWith('+'))
                {
                    current?.Add(new AddedLine(number, line[1..]));
                    number++;
                    newLeft--;
                }
                else if (line.StartsWith('-'))
                {
                    oldLeft--;
                }
                else if (!line.StartsWith('\\'))
                {
                    number++;
                    newLeft--;
                    oldLeft--;
                }

                continue;
            }

            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                // git ends the name with a tab when it holds a space; a deleted file's new side is /dev/null
                var name = line[4..].TrimEnd('\t');
                if (string.Equals(name, "/dev/null", StringComparison.Ordinal))
                {
                    current = null;
                }
                else
                {
                    current = [];
                    files.Add((name.StartsWith("b/", StringComparison.Ordinal) ? name[2..] : name, current));
                }

                continue;
            }

            if (Hunk(line) is var (oldCount, start, newCount))
            {
                oldLeft = oldCount;
                number = start;
                newLeft = newCount;
            }
        }

        return [.. files.Where(static f => f.Lines.Count > 0).Select(static f => new AddedFile(f.File, f.Lines))];
    }

    // '@@ -a[,b] +c[,d] @@': an omitted count is 1.
    private static (int Old, int Start, int New)? Hunk(string line)
    {
        if (!line.StartsWith("@@ -", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = line.Split(' ');
        if (parts.Length < 4 || !parts[1].StartsWith('-') || !parts[2].StartsWith('+'))
        {
            return null;
        }

        var (_, oldCount) = Range(parts[1][1..]);
        var (start, newCount) = Range(parts[2][1..]);
        return (oldCount, start, newCount);
    }

    private static (int Start, int Count) Range(string text)
    {
        var comma = text.IndexOf(',', StringComparison.Ordinal);
        var start = int.Parse(comma < 0 ? text : text[..comma], CultureInfo.InvariantCulture);
        return (start, comma < 0 ? 1 : int.Parse(text[(comma + 1)..], CultureInfo.InvariantCulture));
    }
}
