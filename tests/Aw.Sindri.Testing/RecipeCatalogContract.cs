using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="Core.Recipes.IRecipeCatalog"/> honours: the built-in recipes are listed valid and approved; an unknown id is SND-WF-002.</summary>
public abstract class RecipeCatalogContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A catalog holding the two built-in recipes (workflow.item and workflow.step).</summary>
    /// <returns>The catalog.</returns>
    protected abstract ValueTask<Core.Recipes.IRecipeCatalog> WithBuiltins();

    /// <summary>The built-in recipes are listed, valid and approved, and Get returns them with their hash.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_builtin_recipes_are_listed_valid_and_approved()
    {
        var catalog = await WithBuiltins();

        var listed = (await catalog.List(Token)).Value;
        var step = (await catalog.Get(RecipeId.From("workflow.step"), Token)).Value;

        Expect.Sequence(["workflow.item", "workflow.step", "workflow.ui-evidence"], listed.Select(e => e.Id?.Value ?? "?").Order(StringComparer.Ordinal));
        Expect.True(listed.All(e => e.Valid && e.Approved));
        Expect.Equal(listed.Single(e => e.Id == step.Recipe.Id).Hash, step.Hash);
    }

    /// <summary>An id no recipe has is SND-WF-002.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_recipe_is_refused()
    {
        var catalog = await WithBuiltins();
        Expect.Equal(SindriErrors.RecipeUnknown.Code, (await catalog.Get(RecipeId.From("workflow.nothing"), Token)).Error.Code);
    }
}
