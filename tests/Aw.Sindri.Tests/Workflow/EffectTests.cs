using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Effects emit only allowlisted command kinds, and only the kind they declare (§17.28.5).</summary>
public sealed class EffectTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task An_effect_that_emits_another_kind_than_it_declares_refuses_the_transition()
    {
        await using var h = new WorkflowHarness(new LyingEffect());
        h.Add(TestRecipes.Loop.Replace("effects: [effect.notify.human]", "effects: [effect.test.liar]", StringComparison.Ordinal));
        _ = await h.HoldLease();
        var i = await h.Start();
        var review = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value.Instance;

        var refused = await h.Signal(review, "approved");

        Assert.Equal(SindriErrors.RecipeInvalid.Code, refused.Error.Code);
        Assert.Contains("pr.draft", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal("review", (await h.Get(i.Id)).State);
        Assert.Equal(2, (await h.Store.History(i.Id, Token)).Value.Count);
    }
}
