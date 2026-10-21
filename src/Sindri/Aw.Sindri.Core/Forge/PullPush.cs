namespace Aw.Sindri.Core.Forge;

/// <summary>A push to a pull request's head branch.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="Workspace">The worktree holding the commit; the remote is its origin, accepted only when the host is on the profile's list.</param>
/// <param name="Sha">The commit to push.</param>
/// <param name="LeaseSha">The tip the branch must still have: the head the snapshot saw.</param>
public sealed record PullPush(string Link, string Workspace, string Sha, string LeaseSha);
