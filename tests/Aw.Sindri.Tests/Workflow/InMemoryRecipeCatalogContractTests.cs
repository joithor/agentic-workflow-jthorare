using Aw.Kernel.Testing;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryRecipeCatalogContractTests : RecipeCatalogContract
{
    protected override ValueTask<IRecipeCatalog> WithBuiltins()
    {
        var blocks = Registries.Builtin();
        var catalog = new InMemoryRecipeCatalog(blocks);
        foreach (var name in new[] { "workflow.item", "workflow.step" })
        {
            var (recipe, hash) = TestRecipes.Read(Registries.BuiltinYaml(name));
            catalog.Add(recipe, hash);
        }

        return ValueTask.FromResult<IRecipeCatalog>(catalog);
    }
}
