using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Timers against retries and the universal states.</summary>
public sealed class TimerTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    // A signal named like its state, so a timer key ends the way a signal key would.
    private static readonly string _echo = string.Join(
        '\n',
        "id: workflow.echo",
        "version: 1",
        "initial: claimed",
        "states:",
        "  claimed: { kind: wait, timeout: 10m, onTimeout: retry }",
        "  done: { kind: terminal }",
        "transitions:",
        "  - { from: claimed, to: done, on: claimed }");

    [Fact, Trait("Lane", "unit")]
    public async Task A_timer_that_fired_is_not_mistaken_for_a_retried_signal()
    {
        await using var h = new WorkflowHarness();
        h.Add(_echo);
        _ = await h.HoldLease();
        var i = await h.Start("workflow.echo");
        h.Clock.Advance(TimeSpan.FromMinutes(11));
        _ = (await h.Engine.Tick(clockJump: null, Token)).Value;

        var stale = await h.Signal(i.Id, new WorkflowSignal("claimed", ExpectedVersion: i.Version, Sha: null,
            new Dictionary<string, string>(StringComparer.Ordinal), [], EmptyDiff: false, Spend.Zero));

        Assert.True(stale.IsError, "the signal was taken for a retry of one never recorded");
        Assert.Equal(SindriErrors.InstanceChanged.Code, stale.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Park_take_over_unpark_hand_back_restarts_the_state_timer()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        _ = (await h.Apply(i.Id, new ParkCommand(ParkReason.NeedsAnswer, "a question"))).Value;
        _ = (await h.Apply(i.Id, new TakeOverCommand())).Value;
        _ = (await h.Apply(i.Id, new UnparkCommand(Spend.Zero))).Value;
        h.Clock.Advance(TimeSpan.FromMinutes(3));

        var back = (await h.Apply(i.Id, new HandBackCommand())).Value.Instance;

        Assert.Equal(h.Clock.GetUtcNow() + TimeSpan.FromMinutes(10), back.Deadline);
    }
}
