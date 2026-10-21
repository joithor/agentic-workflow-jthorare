using Aw.Kernel;

namespace Aw.Sindri.Core.Worktrees;

/// <summary>
/// What a workspace changed (port): the commits, the files and the added lines of 'since..HEAD', and the files a branch changed against the repo's default
/// branch. The gates that vet a push read it ('gate.pr.diff-scope', 'gate.pr.secret-scan'), and so does 'effect.pr.push' for the commits it reports. Read only.
/// </summary>
public interface IWorkspaceChanges
{
    /// <summary>The changes between <paramref name="since"/> and the workspace's HEAD.</summary>
    /// <param name="workspace">The worktree.</param>
    /// <param name="since">The commit to compare from (the pull request's head when the fix began).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The changes; an error when the workspace is not a git worktree or <paramref name="since"/> is not a commit of it.</returns>
    public ValueTask<Result<WorkspaceChanges>> Since(string workspace, string since, CancellationToken cancellationToken);

    /// <summary>The files the branch at <paramref name="tip"/> changed against the repo's default branch (the pull request's own changes).</summary>
    /// <param name="workspace">The worktree.</param>
    /// <param name="tip">The commit the branch stood at.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The repo-relative paths, '/' separated; an error when the default branch or the commit can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> BranchFiles(string workspace, string tip, CancellationToken cancellationToken);
}
