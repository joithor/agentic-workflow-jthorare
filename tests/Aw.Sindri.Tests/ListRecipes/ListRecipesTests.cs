using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ListRecipes;

public sealed class ListRecipesTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListRecipes_lists_every_registered_recipe_with_its_hash()
    {
        await using var h = new WorkflowHarness();

        var listed = (await new ListRecipesService(h.Catalog).Handle(new ListRecipesRequest(), WorkflowHarness.Token)).Value.Recipes;

        Assert.Equal([RecipeId.From("workflow.loop"), RecipeId.From("workflow.parent")], listed.Select(r => r.Id!.Value).Order());
        Assert.All(listed, r => Assert.True(r.Valid && r.Hash is not null));
    }
}
