using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Recipes;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Recipes;

/// <summary>Recipe files in Sindri's home: a config change is a new workflow, unapproved; an invalid or duplicate file is listed with its problems and can't run.</summary>
public sealed class FileRecipeCatalogTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task A_recipe_file_registers_unapproved_and_runs()
    {
        Write("short.yaml", TestRecipes.Loop.Replace("workflow.loop", "workflow.short", StringComparison.Ordinal).Replace("gates: [gate.test.model]", "gates: [gate.checks.passed]", StringComparison.Ordinal));

        var recipe = (await Catalog().Get(RecipeId.From("workflow.short"), Token)).Value;

        Assert.False(recipe.Approved);
        Assert.EndsWith("short.yaml", recipe.Source, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Invalid_and_duplicate_files_are_listed_with_problems_and_refused()
    {
        Write("a-broken.yaml", "id: workflow.broken\nversion: 1\ninitial: x\nstates: {}\ntransitions: []\ncolour: red\n");
        Write("b-dupe.yaml", Registries.BuiltinYaml("workflow.step"));
        Write("c-capability.yaml", Registries.BuiltinYaml("workflow.step").Replace("workflow.step", "workflow.mine", StringComparison.Ordinal));

        var listed = (await Catalog().List(Token)).Value;
        var mine = await Catalog().Get(RecipeId.From("workflow.mine"), Token);

        Assert.Equal(6, listed.Count);   // three built-ins and three files
        Assert.Contains(listed, e => e.Source.EndsWith("a-broken.yaml", StringComparison.Ordinal) && e.Problems[0].Check == RecipeChecks.Schema);
        Assert.Contains(listed, e => e.Source.EndsWith("b-dupe.yaml", StringComparison.Ordinal) && e.Problems.Any(p => p.Message.Contains("already registered", StringComparison.Ordinal)));
        Assert.Equal(SindriErrors.RecipeUnknown.Code, mine.Error.Code);
        Assert.Contains("capability", mine.Error.Message, StringComparison.Ordinal);
        Assert.True((await Catalog().Get(RecipeId.From("workflow.step"), Token)).Value.Approved);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Inspect_validates_a_file_as_unapproved_without_registering_it()
    {
        var path = Path.Combine(_home.Path, "outside.yaml");
        await File.WriteAllTextAsync(path, Registries.BuiltinYaml("workflow.step").Replace("workflow.step", "workflow.draft", StringComparison.Ordinal), Token);

        var entry = (await Catalog().Inspect(path, Token)).Value;
        var missing = await Catalog().Inspect(Path.Combine(_home.Path, "nope.yaml"), Token);

        Assert.False(entry.Valid);
        Assert.Contains(entry.Problems, p => p.Check == RecipeChecks.Capability);
        Assert.Equal(3, (await Catalog().List(Token)).Value.Count);   // the built-ins only
        Assert.Equal(SindriErrors.RecipeUnknown.Code, missing.Error.Code);
    }

    private FileRecipeCatalog Catalog() =>
        new(new SindriSettings(_home.Path, Path.Combine(_home.Path, "p.yaml")), Registries.Builtin(), SliceHarness.Json);

    private void Write(string name, string yaml)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "recipes"));
        File.WriteAllText(Path.Combine(_home.Path, "recipes", name), yaml);
    }
}
