namespace Aw.Sindri.Core.Worktrees;

/// <summary>One line a range added.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Line">The line's number in the new file, from 1.</param>
/// <param name="Text">The line, without its line break.</param>
public sealed record AddedLine(string Path, int Line, string Text);
