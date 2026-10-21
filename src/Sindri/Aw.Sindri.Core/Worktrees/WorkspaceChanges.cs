namespace Aw.Sindri.Core.Worktrees;

/// <summary>The changes of 'since..HEAD' in a workspace.</summary>
/// <param name="Head">The workspace's HEAD.</param>
/// <param name="Commits">The commits after the base, oldest first.</param>
/// <param name="Files">Every file the range changed, with how.</param>
/// <param name="AddedLines">Every line the range added, with its file and its line number in the new file.</param>
public sealed record WorkspaceChanges(string Head, IReadOnlyList<WorkspaceCommit> Commits, IReadOnlyList<FileChange> Files, IReadOnlyList<AddedLine> AddedLines);
