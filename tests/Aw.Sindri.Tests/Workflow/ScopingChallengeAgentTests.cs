using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The scoping recipe declares the second opinion's contrast itself (#1269): its challenge state differs from draft, instead of the engine knowing the pair.</summary>
public sealed class ScopingChallengeAgentTests
{
    private static Recipe Scoping() => RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.scoping.yaml"), "workflow.scoping.yaml").Recipe;

    [Fact, Trait("Lane", "unit")]
    public void The_challenge_state_differs_from_the_draft_state() =>
        Assert.Equal(new StateAgent(DifferentFrom: "draft"), Scoping().States["challenge"].Agent);

    [Fact, Trait("Lane", "unit")]
    public void The_scoping_recipe_is_version_18() => Assert.Equal(18, Scoping().Version);

    [Fact, Trait("Lane", "unit")]
    public void No_other_scoping_state_names_an_agent() =>
        Assert.Empty(Scoping().States.Where(s => s.Key != "challenge" && s.Value.Agent is not null).Select(s => s.Key));
}
