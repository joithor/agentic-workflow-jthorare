using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Testing;

namespace Aw.Connectors.Tests.GitHub;

/// <summary>What every <see cref="IGitHubReader"/> does, run against the fake and the real client so the fake stays honest.</summary>
public abstract class GitHubReaderContract
{
    protected const string Repo = "acme/widgets";

    protected static DateTimeOffset T => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected abstract IGitHubReader Reader(IReadOnlyList<GitHubIssue> issues);

    [Fact, Trait("Lane", "unit")]
    public async Task Entries_come_oldest_change_first_and_the_cursor_moves_to_the_newest()
    {
        var reader = Reader([A.GitHubIssue(2).UpdatedAt(T.AddMinutes(5)), A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1))]);

        var page = (await reader.Issues(Repo, GitHubCursor.From(T), 100, Ct)).Value;

        Assert.Equal([1, 2], page.Items.Select(static i => i.Id));
        Assert.Equal(T.AddMinutes(5), page.Next.UpdatedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Entries_the_cursor_has_seen_are_skipped()
    {
        var reader = Reader([A.GitHubIssue(1).UpdatedAt(T), A.GitHubIssue(2).UpdatedAt(T)]);

        var page = (await reader.Issues(Repo, new GitHubCursor(T, [1]), 100, Ct)).Value;

        Assert.Equal([2], page.Items.Select(static i => i.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pull_requests_are_marked()
    {
        var reader = Reader([A.GitHubIssue(1).UpdatedAt(T.AddMinutes(1)).AsPullRequest()]);

        var page = (await reader.Issues(Repo, GitHubCursor.From(T), 100, Ct)).Value;

        Assert.True(Assert.Single(page.Items).IsPullRequest);
    }
}
