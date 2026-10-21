using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Pr;

/// <summary>
/// 'effect.pr.push' with cause 'ci' (PR babysitter spec §10.1): pushes the workspace HEAD to the pull request's head with a lease on the head the snapshot saw. 'pushed', 'pushedSource'
/// (a commit touches non-test source), 'nothing', 'headMoved' and 'rejected'; 'fact:push' always. A retry that finds its own commit as the head reads 'pushed', not 'headMoved'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PushEffectTests
{
    private const string Ours = "c2c2c2c2";
    private const string Identity = "T-3/workflow.pr-babysitter/1:6:0";

    private readonly InMemoryFactStore _facts = new();
    private readonly FakePullRequests _pulls = new();
    private readonly FakeWorkspaceChanges _changes = new();
    private readonly FakeRunDiff _diff = new();

    public PushEffectTests()
    {
        _pulls.Add(PrContexts.Pull());
        _diff.Add(PrContexts.Tree, Ours);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private PushEffect Effect() => new(_pulls, _changes, _diff, _facts, _facts);

    private EffectContext Context(string cause = "ci", bool withWorkspace = true)
    {
        Dictionary<string, string> stored = new(StringComparer.Ordinal) { ["pr"] = $"\"{PrContexts.Link}\"", ["prState"] = PrContexts.State() };
        if (withWorkspace)
        {
            stored["workspace"] = $"\"{PrContexts.Tree}\"";
        }

        return new PrEffectRig(_facts).Context("effect.pr.push", Identity, stored, new Dictionary<string, string>(StringComparer.Ordinal) { ["cause"] = cause });
    }

    private void Committed(params string[] files) =>
        _changes.Set(PrContexts.Tree, new WorkspaceChanges(Ours, [new WorkspaceCommit(Ours, "fix the build\n\nSindri-Cause: ci")], [.. files.Select(static f => new FileChange(f, FileChangeKind.Modified))], []));

    private async Task<EffectDone> Dispatch()
    {
        var done = await Effect().Dispatch(Context(), Token);
        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        return done.Value;
    }

    private JsonNode Pushed(EffectDone done) =>
        JsonNode.Parse(_facts.Stored[done.Facts![FactKey.From("fact:push")].Sha256])!;

    [Fact]
    public async Task A_commit_ahead_of_the_head_is_pushed_with_a_lease_on_the_head_the_snapshot_saw()
    {
        Committed("tests/FooTests.cs");

        var done = await Dispatch();

        Assert.Equal(OutcomeName.From("pushed"), done.Reply);
        Assert.False(done.Existed);
        Assert.Equal([new PullPush(PrContexts.Link, PrContexts.Tree, Ours, PrContexts.Head)], _pulls.Pushes);
        var push = Pushed(done);
        Assert.Equal(Ours, (string?)push["sha"]);
        Assert.Equal([Ours], push["commits"]!.AsArray().Select(static c => (string?)c), StringComparer.Ordinal);
        Assert.Empty(push["comments"]!.AsArray());
        Assert.False((bool)push["source"]!);
        Assert.Equal("ci", (string?)push["cause"]);
        Assert.Equal(PrContexts.Link, (string?)push["pr"]);
    }

    [Fact]
    public async Task A_commit_touching_source_replies_pushedSource_for_the_recheck()
    {
        Committed("src/Foo.cs", "tests/FooTests.cs");

        var done = await Dispatch();

        Assert.Equal(OutcomeName.From("pushedSource"), done.Reply);
        Assert.True((bool)Pushed(done)["source"]!);
    }

    [Fact]
    public async Task A_workspace_still_on_the_head_replies_nothing_and_pushes_nothing()
    {
        _diff.Add(PrContexts.Tree, PrContexts.Head);
        _changes.Set(PrContexts.Tree, new WorkspaceChanges(PrContexts.Head, [], [], []));

        var done = await Dispatch();

        Assert.Equal(OutcomeName.From("nothing"), done.Reply);
        Assert.Empty(_pulls.Pushes);
        Assert.Empty((JsonArray)Pushed(done)["commits"]!);
    }

    [Fact]
    public async Task A_head_that_moved_since_the_snapshot_replies_headMoved()
    {
        Committed("src/Foo.cs");
        _pulls.Change(PrContexts.Link, static p => p with { HeadSha = "bbbb2222" });

        var done = await Dispatch();

        Assert.Equal(OutcomeName.From("headMoved"), done.Reply);
        Assert.False(done.Existed);
        Assert.Equal("bbbb2222", (string?)(await _pulls.Get(PrContexts.Link, Token)).Value.HeadSha);
    }

    [Fact]
    public async Task A_retry_after_the_push_landed_reads_its_own_commit_as_pushed_not_headMoved()
    {
        Committed("src/Foo.cs");
        _pulls.Change(PrContexts.Link, static p => p with { HeadSha = Ours });   // the first attempt pushed, then the reply was lost

        var done = await Dispatch();

        Assert.Equal(OutcomeName.From("pushedSource"), done.Reply);
        Assert.True(done.Existed);
    }

    [Fact]
    public async Task A_remote_that_refuses_the_push_replies_rejected()
    {
        Committed("src/Foo.cs");
        _pulls.PushAnswer = PullPushOutcome.Rejected;

        var done = await Dispatch();

        Assert.Equal(OutcomeName.From("rejected"), done.Reply);
    }

    [Fact]
    public async Task An_unreachable_forge_is_an_error_the_activity_retries()
    {
        Committed("src/Foo.cs");
        var gone = new FakePullRequests();   // knows no pull request: the push errors

        var result = await new PushEffect(gone, _changes, _diff, _facts, _facts).Dispatch(Context(), Token);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task A_cause_other_than_ci_is_refused_without_retry()
    {
        Committed("src/Foo.cs");

        var result = await Effect().Dispatch(Context(cause: "review"), Token);

        Assert.True(result.IsError);
        Assert.Equal(WorkflowErrors.NoRuntime.Code, result.Error.Code);
        Assert.Contains("review", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_pulls.Pushes);
    }

    [Fact]
    public async Task A_missing_workspace_is_refused_without_retry()
    {
        var result = await Effect().Dispatch(Context(withWorkspace: false), Token);

        Assert.True(result.IsError);
        Assert.Equal(WorkflowErrors.NoRuntime.Code, result.Error.Code);
        Assert.Contains("workspace", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_dispatch_throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () => await Effect().Dispatch(Context(), cancelled.Token));
    }
}
