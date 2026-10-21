namespace Aw.Sindri.Core.Worktrees;

/// <summary>One commit of a range.</summary>
/// <param name="Sha">The commit.</param>
/// <param name="Message">Its whole message, subject and body.</param>
public sealed record WorkspaceCommit(string Sha, string Message);
