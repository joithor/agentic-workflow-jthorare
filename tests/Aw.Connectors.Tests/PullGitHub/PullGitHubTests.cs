using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Features;
using Aw.Connectors.Features.PullGitHub;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.PullGitHub;

public sealed class PullGitHubTests
{
    private const string Repo = "acme/widgets";
    private static DateTimeOffset T => new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IService<PullGitHubRequest, PullGitHubResponse> Service(FakeGitHubReader reader)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IGitHubReader>(reader).AddConnectorsFeatures();
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IService<PullGitHubRequest, PullGitHubResponse>>();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_issue_becomes_a_record_with_its_key_permalink_and_cleaned_text()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(7).UpdatedAt(T.AddHours(1)).WithBody("<!-- note -->Crash on **save**.\r\n\r\n![shot](x.png)"));

        var response = (await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value;

        var item = Assert.Single(response.Items);
        Assert.Equal("github:issue/acme/widgets/7", item.Key);
        Assert.Equal("issue", item.Kind);
        Assert.Equal("Issue 7", item.Title);
        Assert.Equal("Issue 7\n\nCrash on save.", item.Text);
        Assert.Equal("https://github.com/acme/widgets/issues/7", item.Url);
        Assert.Equal("open", item.State);
        Assert.Equal("octocat", item.Author);
        Assert.Equal(Repo, item.Repo);
        Assert.Equal("2026-10-01T13:00:00.0000000Z", item.Version);
        Assert.Empty(item.Links);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_is_keyed_as_pr_and_links_the_issues_it_closes()
    {
        var reader = new FakeGitHubReader().Add(
            Repo,
            A.GitHubIssue(12).AsPullRequest().Closed().UpdatedAt(T.AddHours(1)).WithBody("Fixes #3 and closes #4. Also fixes #12 (itself) and fixes #3 again."));

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items);

        Assert.Equal("github:pr/acme/widgets/12", item.Key);
        Assert.Equal("pr", item.Kind);
        Assert.Equal("closed", item.State);
        Assert.True(item.Links.Select(static l => l.Key).SequenceEqual(["github:issue/acme/widgets/3", "github:issue/acme/widgets/4"], StringComparer.Ordinal));
        Assert.All(item.Links, static l => Assert.Equal("closes", l.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_issue_never_links_even_when_its_body_says_fixes()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(5).UpdatedAt(T.AddHours(1)).WithBody("Fixes #3"));

        var item = Assert.Single((await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value.Items);

        Assert.Empty(item.Links);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_next_cursor_and_the_rate_limit_come_through()
    {
        var reader = new FakeGitHubReader().Add(Repo, A.GitHubIssue(1).UpdatedAt(T.AddHours(2)));

        var response = (await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct)).Value;

        Assert.Equal(T.AddHours(2), response.Next.UpdatedAt);
        Assert.Equal<long>([1], response.Next.SeenIds);
        Assert.Null(response.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_request_reaches_the_reader_unchanged()
    {
        var reader = new FakeGitHubReader();
        var after = new GitHubCursor(T, [9]);

        _ = await Service(reader).Handle(A.PullGitHubRequest().After(after).WithLimit(25).Build(), Ct);

        Assert.Equal((Repo, after, 25), Assert.Single(reader.Calls));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reader_error_comes_back_as_the_error()
    {
        var reader = new FakeGitHubReader().Fail(ConnectorsErrors.Unauthorized.With("rejected"));

        var result = await Service(reader).Handle(A.PullGitHubRequest().Build(), Ct);

        Assert.True(result.IsError);
        Assert.Equal(ConnectorsErrors.Unauthorized.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("widgets", 100)]
    [InlineData("acme/widgets/extra", 100)]
    [InlineData("acme/widgets", 0)]
    [InlineData("acme/widgets", 10001)]
    public async Task A_bad_repo_or_limit_is_refused_before_any_read(string repo, int limit)
    {
        var reader = new FakeGitHubReader();

        var result = await Service(reader).Handle(A.PullGitHubRequest().ForRepo(repo).WithLimit(limit).Build(), Ct);

        Assert.Equal(ConnectorsErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(reader.Calls);
    }
}
