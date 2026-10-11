using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IWorkflowStore"/> honours (factory addendum §17.20, §17.28.1): a commit is fenced by the
/// lease epoch, idempotent by its first event's key, versioned per instance, and atomic across its writes and outbox
/// commands; reads return rows, due timers, children and each instance's events.
/// </summary>
public abstract class WorkflowStoreContract
{
    private readonly SequentialIdGenerator _ids = new();

    /// <summary>The contract's clock.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>The probe; this process is pid 100.</summary>
    protected FakeProcessProbe Probe { get; } = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The stores over a fresh, empty file or memory.</summary>
    /// <param name="clock">The only clock.</param>
    /// <param name="probe">The process probe.</param>
    /// <returns>The stores.</returns>
    protected abstract ValueTask<WorkflowStores> Create(TimeProvider clock, IProcessProbe probe);

    /// <summary>A committed start reads back as the row, in the list and as one event in its history.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_started_instance_reads_back_with_its_event()
    {
        var (lease, store, _) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var instance = AnInstance.New("ITEM-1", Clock.GetUtcNow());

        var outcome = (await store.Commit(Commit(Write(instance, "k-start", expected: 0)), grant, Token)).Value;

        Expect.True(!outcome.AlreadyRecorded);
        Expect.Equal(instance.Id, (await store.Get(instance.Id, Token)).Value?.Id);
        Expect.Equal(1, (await store.List(new InstanceQuery(IncludeEnded: false), Token)).Value.Count);
        var only = Expect.Only((await store.History(instance.Id, Token)).Value);
        Expect.Equal(("k-start", grant.Epoch, (ItemId?)instance.Item), (only.IdempotencyKey, only.Epoch, only.Item));
    }

    /// <summary>A commit whose first key is recorded already writes nothing and reports it (a retry or replay is a no-op).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_replayed_commit_writes_nothing()
    {
        var (lease, store, commands) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var instance = AnInstance.New("ITEM-1", Clock.GetUtcNow());
        var commit = Commit(Write(instance, "k-start", expected: 0), withCommand: true);
        var first = (await store.Commit(commit, grant, Token)).Value;

        var again = (await store.Commit(commit, grant, Token)).Value;

        Expect.Equal((true, first.Sequences[0]), (again.AlreadyRecorded, again.Sequences[0]));
        _ = Expect.Only((await store.History(instance.Id, Token)).Value);
        _ = Expect.Only(await commands());
    }

    /// <summary>A commit under an epoch that isn't current is refused and writes nothing (fencing).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_commit_with_a_stale_epoch_is_refused()
    {
        var (lease, store, commands) = await Create(Clock, Probe);
        var old = (await lease.Acquire(Probe.Self(), Token)).Value;
        _ = await lease.Release(old, Token);
        _ = (await lease.Acquire(Probe.Self(), Token)).Value;
        var instance = AnInstance.New("ITEM-1", Clock.GetUtcNow());

        var refused = await store.Commit(Commit(Write(instance, "k-start", expected: 0), withCommand: true), old, Token);

        Expect.Equal(SindriErrors.LeaseStale.Code, refused.Error.Code);
        Expect.True((await store.Get(instance.Id, Token)).Value is null);
        Expect.Equal(0, (await commands()).Count);
    }

    /// <summary>A write expecting a version the instance no longer has is refused, and the whole commit (every write, every command) with it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_commit_against_a_changed_version_is_refused_whole()
    {
        var (lease, store, commands) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var a = AnInstance.New("ITEM-A", Clock.GetUtcNow());
        var b = AnInstance.New("ITEM-B", Clock.GetUtcNow());
        _ = (await store.Commit(Commit(Write(a, "a-start", expected: 0)), grant, Token)).Value;
        var newB = Write(b, "b-start", expected: 0);
        var staleA = Write(a with { Version = 2, State = "next" }, "a-next", expected: 5);

        var refused = await store.Commit(new WorkflowCommit([newB, staleA], [Command()]), grant, Token);

        Expect.Equal(SindriErrors.InstanceChanged.Code, refused.Error.Code);
        Expect.True((await store.Get(b.Id, Token)).Value is null);
        Expect.Equal(1L, (await store.Get(a.Id, Token)).Value?.Version);
        Expect.Equal(0, (await commands()).Count);
    }

    /// <summary>One commit writes several instances (a transition and the child it starts) and its outbox commands together.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_commit_writes_every_instance_and_command_together()
    {
        var (lease, store, commands) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var parent = AnInstance.New("ITEM-1", Clock.GetUtcNow());
        var child = AnInstance.New("ITEM-1", Clock.GetUtcNow(), recipe: "workflow.child") with { Parent = parent.Id };

        var outcome = (await store.Commit(new WorkflowCommit([Write(parent, "p", 0), Write(child, "c", 0)], [Command(), Command()]), grant, Token)).Value;

        Expect.Equal(2, outcome.Sequences.Count);
        Expect.Equal(child.Id, Expect.Only((await store.Children(parent.Id, Token)).Value).Id);
        Expect.Equal(2, (await commands()).Count);
    }

    /// <summary>Due lists running instances whose deadline has come, earliest first; held, taken-over and ended ones never.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Due_lists_running_instances_whose_timer_came()
    {
        var (lease, store, _) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var now = Clock.GetUtcNow();
        var late = AnInstance.New("LATE", now) with { Deadline = now.AddMinutes(-2) };
        var soon = AnInstance.New("SOON", now) with { Deadline = now.AddMinutes(-1) };
        var future = AnInstance.New("FUTURE", now) with { Deadline = now.AddMinutes(5) };
        var taken = AnInstance.New("TAKEN", now) with { Deadline = now.AddMinutes(-3), TakenOver = true };
        var parked = AnInstance.New("PARKED", now) with { Deadline = now.AddMinutes(-3), Hold = new InstanceHold(HoldKind.Parked, ParkReason.Timeout, "x") };
        var writes = new[] { late, soon, future, taken, parked }.Select(i => Write(i, i.Item.Value, 0)).ToList();
        _ = (await store.Commit(new WorkflowCommit(writes, []), grant, Token)).Value;

        var due = (await store.Due(now, 10, Token)).Value;

        Expect.Sequence(["LATE", "SOON"], due.Select(i => i.Item.Value));
    }

    private static WorkflowCommit Commit(InstanceWrite write, bool withCommand = false) => new([write], withCommand ? [Command()] : []);

    private static Kernel.Outbox.OutboxCommand Command() =>
        EventContext.Create("host:sindri", "local", 0, new SequentialIdGenerator().NewId()).Value
            .Command("notify.human", """{"n":1}""", new FakeTimeProvider(KnownInstants.Epoch), new UniqueIds());

    private InstanceWrite Write(WorkflowInstance after, string key, long expected)
    {
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        return new InstanceWrite(new LedgerEntry(context.Envelope("workflow.started", """{"n":1}""", Clock, _ids), key, after.Item), after, expected);
    }
}
