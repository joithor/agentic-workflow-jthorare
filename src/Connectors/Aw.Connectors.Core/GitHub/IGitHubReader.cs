using Aw.Kernel;

namespace Aw.Connectors.Core.GitHub;

/// <summary>Reads a repo's issues and pull requests changed since a cursor.</summary>
public interface IGitHubReader
{
    /// <summary>Reads the entries of <paramref name="repo"/> changed at or after the cursor, skipping the ones the cursor has seen. Whole pages are read, so slightly more than <paramref name="limit"/> entries can come back.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="after">Where to start; <see cref="GitHubCursor.From"/> for a first run.</param>
    /// <param name="limit">Stop after the page that brings the run to this many entries.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The page (a rate limit is a value in it, not an error), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, CancellationToken cancellationToken);
}
