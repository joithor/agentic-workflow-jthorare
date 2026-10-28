using Aw.Connectors.Core.GitHub;
using Aw.Kernel;

namespace Aw.Connectors.Testing.GitHub;

/// <summary>An in-memory <see cref="IGitHubReader"/>: lists what was added per repo oldest change first, skips what the cursor has seen and reads whole pages like the real client (contract-tested against it).</summary>
public sealed class FakeGitHubReader : IGitHubReader
{
    private readonly Dictionary<string, List<GitHubIssue>> _issues = new(StringComparer.Ordinal);
    private readonly List<(string Repo, GitHubCursor After, int Limit)> _calls = [];
    private Error? _failure;

    /// <summary>The calls made, in order.</summary>
    public IReadOnlyList<(string Repo, GitHubCursor After, int Limit)> Calls => _calls;

    /// <summary>Gets the page size the fake reads in.</summary>
    public int PageSize { get; init; } = 100;

    /// <summary>Adds an entry.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="issue">The entry.</param>
    /// <returns>This fake.</returns>
    public FakeGitHubReader Add(string repo, GitHubIssue issue)
    {
        if (!_issues.TryGetValue(repo, out var list))
        {
            list = [];
            _issues[repo] = list;
        }

        list.Add(issue);
        return this;
    }

    /// <summary>Makes every read fail with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>This fake.</returns>
    public FakeGitHubReader Fail(Error error)
    {
        _failure = error;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, CancellationToken cancellationToken)
    {
        _calls.Add((repo, after, limit));
        if (_failure is not null)
        {
            return ValueTask.FromResult<Result<GitHubPullPage>>(_failure);
        }

        var all = (_issues.TryGetValue(repo, out var list) ? list : [])
            .Where(after.Admits)
            .OrderBy(static i => i.UpdatedAt)
            .ThenBy(static i => i.Id)
            .ToList();
        var taken = new List<GitHubIssue>();
        for (var start = 0; start < all.Count && taken.Count < limit; start += PageSize)
        {
            taken.AddRange(all.Skip(start).Take(PageSize));
        }

        return ValueTask.FromResult<Result<GitHubPullPage>>(new GitHubPullPage(taken, after.Advance(taken), RateLimitedUntil: null));
    }
}
