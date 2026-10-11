using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>An instance whose recipe changed under it: the human can still cancel, park, take over and register evidence, and the tick escalates it once.</summary>
public sealed class StrandedRecipeTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task A_stranded_instance_can_still_be_cancelled()
    {
        await using var h = await Stranded();
        var i = (await h.Store.List(new InstanceQuery(IncludeEnded: false, Max: 10), Token)).Value[0];

        var cancelled = await h.Apply(i.Id, new CancelCommand("recipe changed"));

        Assert.True(cancelled.IsSuccess, cancelled.IsError ? cancelled.Error.Message : null);
        Assert.Equal(InstanceStatus.Cancelled, cancelled.Value.Instance.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stranded_instance_can_be_parked_taken_over_handed_back_and_given_evidence()
    {
        await using var h = await Stranded();
        var i = (await h.Store.List(new InstanceQuery(IncludeEnded: false, Max: 10), Token)).Value[0];

        var evidence = await h.Apply(i.Id, new RegisterEvidenceCommand("test", HeadSha.From("abc1234"), "sha256:one"));
        var taken = await h.Apply(i.Id, new TakeOverCommand());
        var back = await h.Apply(i.Id, new HandBackCommand());
        var parked = await h.Apply(i.Id, new ParkCommand(ParkReason.NeedsAnswer, "which recipe?"));
        var unparked = await h.Apply(i.Id, new UnparkCommand(Spend.Zero));

        Assert.True(evidence.IsSuccess && taken.IsSuccess && back.IsSuccess && parked.IsSuccess);
        Assert.Equal(SindriErrors.RecipeChanged.Code, unparked.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_tick_escalates_a_stranded_instance_once_so_it_leaves_the_due_batch()
    {
        await using var h = await Stranded();
        h.Clock.Advance(TimeSpan.FromMinutes(11));

        var first = (await h.Engine.Tick(clockJump: null, Token)).Value;
        var second = (await h.Engine.Tick(clockJump: null, Token)).Value;

        var i = (await h.Store.List(new InstanceQuery(IncludeEnded: false, Max: 10), Token)).Value[0];
        Assert.Equal((1, 0), (first.Fired, second.Fired));
        Assert.Equal(InstanceStatus.Escalated, i.Status);
        Assert.Contains("recipe changed", i.Hold?.Detail, StringComparison.Ordinal);
        Assert.Empty((await h.Store.Due(h.Clock.GetUtcNow(), 10, Token)).Value);
    }

    private static async Task<WorkflowHarness> Stranded()
    {
        var h = new WorkflowHarness();
        _ = await h.HoldLease();
        _ = await h.Start();
        h.Add(TestRecipes.Loop.Replace("cap: 2", "cap: 3", StringComparison.Ordinal));
        return h;
    }
}
