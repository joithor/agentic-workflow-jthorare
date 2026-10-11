using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Recipes;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ValidateRecipe;

public sealed class ValidateRecipeTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private ValidateRecipeService Service() =>
        new(new FileRecipeCatalog(new SindriSettings(_home.Path, Path.Combine(_home.Path, "p.yaml")), Registries.Builtin(), SliceHarness.Json));

    [Fact, Trait("Lane", "integration")]
    public async Task ValidateRecipe_of_a_good_file_gives_its_id_and_hash_unapproved()
    {
        var path = Path.Combine(_home.Path, "mine.yaml");
        await File.WriteAllTextAsync(path, TestRecipes.Loop.Replace("workflow.loop", "workflow.mine", StringComparison.Ordinal).Replace("gates: [gate.test.model]", "gates: [gate.checks.passed]", StringComparison.Ordinal), TestContext.Current.CancellationToken);

        var entry = (await Service().Handle(new ValidateRecipeRequest(path), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(RecipeId.From("workflow.mine"), entry.Id);
        Assert.False(entry.Approved);
        Assert.NotNull(entry.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task ValidateRecipe_of_a_broken_file_lists_its_problems()
    {
        var path = Path.Combine(_home.Path, "broken.yaml");
        await File.WriteAllTextAsync(path, "id: workflow.broken\nversion: 1\ninitial: x\nstates: {}\ntransitions: []\ncolour: red\n", TestContext.Current.CancellationToken);

        var entry = (await Service().Handle(new ValidateRecipeRequest(path), TestContext.Current.CancellationToken)).Value;

        Assert.False(entry.Valid);
        Assert.Equal(RecipeChecks.Schema, entry.Problems[0].Check);
    }
}
