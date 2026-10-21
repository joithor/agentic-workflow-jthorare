using Aw.Kernel;

namespace Aw.Sindri.Core.Forge;

/// <summary>
/// The pull requests the line follows (PR babysitter spec PB10, forge wiring brief item 1): Sindri's own port over the forge, with only what the line needs
/// and Sindri's types and strings. Its adapter sits in the Host, the one project that may reference the forge package. Everything it returns from the forge
/// (a check's log, a title) is untrusted text. This build declares the members the CI-red autofix loop reads and writes; the forge wiring unit adds the rest
/// (Open, Find, Digests) beside them.
/// </summary>
public interface IPullRequests
{
    /// <summary>Reads a pull request: its state, its head, its checks and what asks for attention.</summary>
    /// <param name="link">The pull request's link, as 'fact:pr' holds it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pull request; an error when the forge can't be reached, the link is refused, or the profile has no forge.</returns>
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken);

    /// <summary>Reads the tail of one check's log, capped and scrubbed by the forge port. The text is untrusted.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="check">The check's name, as <see cref="PullChecks.Failing"/> lists it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The excerpt.</returns>
    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken);

    /// <summary>Pushes a workspace commit to the pull request's head branch, with a lease on the tip the caller saw. Never rebases, never forces past the lease.</summary>
    /// <param name="push">The push.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    /// <returns>How it ended when git itself worked; an error otherwise.</returns>
    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken);

    /// <summary>Asks the forge to run the failed checks of a commit again.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="sha">The commit whose failed checks run again.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many checks were asked to run again.</returns>
    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken);
}
