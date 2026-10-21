using Aw.Sindri.Core.Forge;
using Xunit;

namespace Aw.Sindri.Testing.Forge;

/// <summary>
/// What every <see cref="IPullRequests"/> honours: a pull request it knows reads back with its head and checks; an unknown one is an error; a check's log reads back; a push whose
/// lease is the head moves it and any other lease is HeadMoved and moves nothing; a rerun answers a count. A binding supplies the adapter over a world with one pull request
/// whose head is 'aaaa' and whose check 'build' failed on it with the log 'boom'.
/// </summary>
public abstract class PullRequestsContract
{
    /// <summary>The adapter, the link of its pull request and a workspace the push can read.</summary>
    /// <returns>The adapter, the link and the workspace.</returns>
    protected abstract ValueTask<(IPullRequests Pulls, string Link, string Workspace)> Create();

    /// <summary>A commit to push on top of the head 'aaaa'.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <returns>The new commit's sha.</returns>
    protected abstract ValueTask<string> Commit(string workspace);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The pull request reads back.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pull_request_reads_back_with_its_head_and_its_failing_check()
    {
        var (pulls, link, _) = await Create();

        var read = await pulls.Get(link, Token);

        Expect.True(read.IsSuccess);
        Expect.Equal("aaaa", read.Value.HeadSha);
        Expect.Equal(PullState.Open, read.Value.State);
        Expect.Equal(PullChecksState.Failing, read.Value.Checks.State);
        Expect.Sequence(["build"], read.Value.Checks.Failing);
    }

    /// <summary>An unknown pull request is an error, never an empty view.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_pull_request_is_an_error()
    {
        var (pulls, link, _) = await Create();

        Expect.True((await pulls.Get(link + "9999", Token)).IsError);
    }

    /// <summary>A check's log reads back.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_check_log_reads_back()
    {
        var (pulls, link, _) = await Create();

        var log = await pulls.CheckLog(link, "build", Token);

        Expect.True(log.IsSuccess);
        Expect.True(log.Value.Contains("boom", StringComparison.Ordinal));
    }

    /// <summary>A push on the lease moves the head.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_push_on_the_lease_moves_the_head()
    {
        var (pulls, link, workspace) = await Create();
        var sha = await Commit(workspace);

        var pushed = await pulls.Push(new PullPush(link, workspace, sha, "aaaa"), Token);
        var read = await pulls.Get(link, Token);

        Expect.True(pushed.IsSuccess);
        Expect.Equal(PullPushOutcome.Pushed, pushed.Value);
        Expect.Equal(sha, read.Value.HeadSha);
    }

    /// <summary>A push on a stale lease is refused and moves nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_push_on_a_stale_lease_is_head_moved_and_moves_nothing()
    {
        var (pulls, link, workspace) = await Create();
        var sha = await Commit(workspace);

        var pushed = await pulls.Push(new PullPush(link, workspace, sha, "bbbb"), Token);
        var read = await pulls.Get(link, Token);

        Expect.True(pushed.IsSuccess);
        Expect.Equal(PullPushOutcome.HeadMoved, pushed.Value);
        Expect.Equal("aaaa", read.Value.HeadSha);
    }

    /// <summary>A rerun answers how many checks it asked for.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_rerun_counts_the_failed_checks()
    {
        var (pulls, link, _) = await Create();

        var count = await pulls.RerunChecks(link, "aaaa", Token);

        Expect.True(count.IsSuccess);
        Expect.Equal(1, count.Value);
    }
}
