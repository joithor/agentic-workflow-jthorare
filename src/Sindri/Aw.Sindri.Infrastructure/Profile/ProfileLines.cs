using System.Text.RegularExpressions;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// A YAML file as lines, for an edit that keeps everything it doesn't touch: the line ending the file uses (CRLF or LF), whether it ends with one, and a few questions about
/// block-style maps by indentation (where a key's block ends, where its children are indented). It reads no YAML: <see cref="ProfileEdit"/> parses the file for that.
/// </summary>
internal sealed partial class ProfileLines
{
    private ProfileLines(List<string> lines, string newline, bool trailingNewline)
    {
        Lines = lines;
        Newline = newline;
        TrailingNewline = trailingNewline;
    }

    /// <summary>Gets the lines, without their line endings.</summary>
    internal List<string> Lines { get; }

    /// <summary>Gets the line ending the file uses.</summary>
    internal string Newline { get; }

    /// <summary>Gets a value indicating whether the file ended with a line ending.</summary>
    internal bool TrailingNewline { get; }

    /// <summary>Splits a file into lines.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The lines.</returns>
    internal static ProfileLines Parse(string text)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var trailing = text.EndsWith('\n');
        var lines = new List<string>(text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));
        if (trailing)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return new ProfileLines(lines, newline, trailing);
    }

    /// <summary>Joins the lines back with the file's own line ending.</summary>
    /// <returns>The text.</returns>
    internal string Render() => string.Join(Newline, Lines) + (TrailingNewline ? Newline : string.Empty);

    /// <summary>The number of leading spaces of a line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The indent.</returns>
    internal static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    /// <summary>Finds a map key written at exactly <paramref name="indent"/> among lines [from, to).</summary>
    /// <param name="from">The first line to look at.</param>
    /// <param name="to">The end (exclusive).</param>
    /// <param name="indent">The key's indent.</param>
    /// <param name="name">The key.</param>
    /// <returns>The line's index and what follows the colon (comment removed, trimmed), or null.</returns>
    internal (int Index, string Remainder)? FindKey(int from, int to, int indent, string name)
    {
        for (var i = from; i < to; i++)
        {
            if (IsContent(Lines[i]) && Indent(Lines[i]) == indent && Key.Match(Lines[i]) is { Success: true } m && string.Equals(m.Groups["name"].Value, name, StringComparison.Ordinal))
            {
                return (i, StripComment(m.Groups["rest"].Value).Trim());
            }
        }

        return null;
    }

    /// <summary>The end (exclusive) of the block under the key at <paramref name="keyIndex"/>: the next content line indented no deeper than the key, or the end of the file.</summary>
    /// <param name="keyIndex">The key's line.</param>
    /// <returns>The block's end.</returns>
    internal int BlockEnd(int keyIndex)
    {
        var indent = Indent(Lines[keyIndex]);
        var i = keyIndex + 1;
        while (i < Lines.Count && (!IsContent(Lines[i]) || Indent(Lines[i]) > indent))
        {
            i++;
        }

        return i;
    }

    /// <summary>The last content line of the block under the key, or the key's own line when it has none.</summary>
    /// <param name="keyIndex">The key's line.</param>
    /// <param name="end">The block's end.</param>
    /// <returns>The line's index.</returns>
    internal int LastContent(int keyIndex, int end)
    {
        for (var i = end - 1; i > keyIndex; i--)
        {
            if (IsContent(Lines[i]))
            {
                return i;
            }
        }

        return keyIndex;
    }

    /// <summary>The indent of the first content line under the key, or null when it has none.</summary>
    /// <param name="keyIndex">The key's line.</param>
    /// <param name="end">The block's end.</param>
    /// <returns>The indent.</returns>
    internal int? ChildIndent(int keyIndex, int end)
    {
        for (var i = keyIndex + 1; i < end; i++)
        {
            if (IsContent(Lines[i]))
            {
                return Indent(Lines[i]);
            }
        }

        return null;
    }

    private static bool IsContent(string line) => line.Trim().Length > 0 && !line.TrimStart(' ').StartsWith('#');

    // Text after a ' #' is a comment (a '#' glued to a word isn't).
    private static string StripComment(string text)
    {
        var at = text.IndexOf(" #", StringComparison.Ordinal);
        return at < 0 ? text : text[..at];
    }

    [GeneratedRegex(@"^ *(?<name>[A-Za-z_][A-Za-z0-9_-]*)\s*:(?<rest>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Key { get; }
}
