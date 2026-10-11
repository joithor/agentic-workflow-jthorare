using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The engine properties of factory addendum §17.28.5.</summary>
public sealed class EnginePropertyTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task Replaying_the_event_log_reproduces_the_instance()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        _ = (await h.Signal(i.Id, Round(["a.cs:CS1:x"]))).Value;
        _ = (await h.Engine.Apply(i.Id, new TakeOverCommand(), Token)).Value;
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = (await h.Engine.Apply(i.Id, new HandBackCommand(), Token)).Value;
        _ = (await h.Engine.Apply(i.Id, new RegisterEvidenceCommand("test", Core.Ids.HeadSha.From("abc1234"), "sha256:1"), Token)).Value;
        _ = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value;
        _ = (await h.Engine.Apply(i.Id, new ParkCommand(ParkReason.NeedsAnswer, "which API?"), Token)).Value;
        _ = (await h.Engine.Apply(i.Id, new UnparkCommand(new Spend(10, 1)), Token)).Value;

        var history = (await h.Store.History(i.Id, Token)).Value;
        WorkflowInstance? replayed = null;
        foreach (var e in history)
        {
            replayed = InstanceFold.Apply(replayed, AwJson.Deserialize<WorkflowChange>(Encoding.UTF8.GetBytes(e.Envelope.Payload), SliceHarness.Json).Value);
        }

        Assert.Equal(8, history.Count);
        Assert.Equal(Json(await h.Get(i.Id)), Json(replayed!));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Applying_the_same_signal_twice_is_a_no_op()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var signal = Round(["a.cs:CS1:x"]) with { ExpectedVersion = 1 };

        var first = (await h.Signal(i.Id, signal)).Value;
        var again = (await h.Signal(i.Id, signal)).Value;
        var restart = (await h.Engine.Start(i.Recipe, i.Item, 1, baseSha: null, Token)).Value;

        Assert.False(first.AlreadyRecorded);
        Assert.True(again.AlreadyRecorded);
        Assert.True(restart.AlreadyRecorded);
        Assert.Equal(2, (await h.Store.History(i.Id, Token)).Value.Count);
        Assert.Equal(2, (await h.Get(i.Id)).Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_epoch_is_refused_and_writes_nothing()
    {
        await using var h = new WorkflowHarness();
        var old = await h.HoldLease();
        var i = await h.Start();
        _ = await h.Lease.Release(old, Token);
        _ = (await h.Lease.Acquire(h.Probe.Self(), Token)).Value;

        var refused = await h.Signal(i, "passed", ("checks.verdict", "pass"));

        Assert.Equal(SindriErrors.LeaseStale.Code, refused.Error.Code);
        Assert.Equal(1, (await h.Get(i.Id)).Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_instance_version_is_refused()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        _ = (await h.Signal(i.Id, Round(["a.cs:CS1:x"]))).Value;

        var refused = await h.Signal(i.Id, new WorkflowSignal("passed", 1, Sha: null, Outputs("pass"), [], EmptyDiff: false, Spend.Zero));

        Assert.Equal(SindriErrors.InstanceChanged.Code, refused.Error.Code);
        Assert.Equal("work", (await h.Get(i.Id)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_timer_firing_during_a_takeover_does_nothing_and_resumes_after_handback()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        h.Clock.Advance(TimeSpan.FromMinutes(4));
        _ = (await h.Engine.Apply(i.Id, new TakeOverCommand(), Token)).Value;
        h.Clock.Advance(TimeSpan.FromHours(2));

        var during = (await h.Engine.Tick(clockJump: null, Token)).Value;
        var handedBack = (await h.Engine.Apply(i.Id, new HandBackCommand(), Token)).Value.Instance;
        h.Clock.Advance(TimeSpan.FromMinutes(6));
        var after = (await h.Engine.Tick(clockJump: null, Token)).Value;

        Assert.Equal(0, during.Fired);
        Assert.Equal(h.Clock.GetUtcNow() - TimeSpan.FromMinutes(6) + TimeSpan.FromMinutes(6), handedBack.Deadline);
        Assert.Equal(1, after.Fired);
        Assert.Equal(1, (await h.Get(i.Id)).Retries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_park_propagates_to_the_parent_and_its_unpark_lifts_it()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;
        var child = Assert.Single((await h.Store.Children(parent.Id, Token)).Value);

        _ = (await h.Signal(child.Id, Round(["a.cs:CS1:x"]))).Value;
        var parked = (await h.Signal(child.Id, Round(["a.cs:CS1:x"]))).Value.Instance;

        Assert.Equal(ParkReason.NoProgress, parked.Hold?.Reason);
        var p = await h.Get(parent.Id);
        Assert.Equal((InstanceStatus.Parked, ParkReason.NoProgress, (Core.Ids.InstanceId?)child.Id), (p.Status, p.Hold?.Reason, p.Hold?.Child));

        _ = (await h.Engine.Apply(child.Id, new UnparkCommand(Spend.Zero), Token)).Value;
        Assert.Equal(InstanceStatus.Active, (await h.Get(parent.Id)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_escalation_propagates_to_the_parent()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;
        var child = Assert.Single((await h.Store.Children(parent.Id, Token)).Value);
        _ = (await h.Signal(child, "passed", ("checks.verdict", "pass"))).Value;

        var escalated = (await h.Signal(child, "risky")).Value;

        Assert.Equal(InstanceStatus.Escalated, escalated.Instance.Status);
        Assert.False(Assert.Single(escalated.Gates).Passed);
        Assert.Equal(InstanceStatus.Escalated, (await h.Get(parent.Id)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_completion_moves_the_parent_on_child_done_in_one_commit()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;
        var child = Assert.Single((await h.Store.Children(parent.Id, Token)).Value);
        _ = (await h.Signal(child, "passed", ("checks.verdict", "pass"))).Value;

        _ = (await h.Signal(child, "approved")).Value;

        Assert.Equal((InstanceStatus.Done, "done"), ((await h.Get(parent.Id)).Status, (await h.Get(parent.Id)).State));
        var command = Assert.Single(h.Store.Commands);
        var completing = h.Store.Events.Single(e => e.IdempotencyKey.Contains("review>done", StringComparison.Ordinal));
        Assert.Equal(("notify.human", (Guid?)completing.Envelope.Id), (command.Kind, command.CausationId));
        Assert.Equal(completing.Envelope.Id, h.Store.Events[^1].Envelope.CausationId);
    }

    private static WorkflowSignal Round(IReadOnlyList<string> findings) =>
        new("failed", ExpectedVersion: null, Sha: null, Outputs("fail"), findings, EmptyDiff: false, Spend.Zero);

    private static Dictionary<string, string> Outputs(string verdict) => new(StringComparer.Ordinal) { ["checks.verdict"] = verdict };

    private static string Json(WorkflowInstance i) => Encoding.UTF8.GetString(AwJson.Serialize(i, SliceHarness.Json));
}
