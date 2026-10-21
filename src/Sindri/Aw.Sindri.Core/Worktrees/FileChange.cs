namespace Aw.Sindri.Core.Worktrees;

/// <summary>One file a range changed.</summary>
/// <param name="Path">The repo-relative path, '/' separated; a renamed file's new path.</param>
/// <param name="Kind">How it changed.</param>
public sealed record FileChange(string Path, FileChangeKind Kind);
