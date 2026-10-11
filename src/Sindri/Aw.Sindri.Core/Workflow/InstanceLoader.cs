using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>Loads an instance and its recipe, refusing one whose recipe changed under it (§17.28.1: a recipe is versioned and hashed; an instance keeps the one it started with).</summary>
/// <param name="store">The workflow store.</param>
/// <param name="catalog">The recipe catalog.</param>
internal sealed class InstanceLoader(IWorkflowStore store, IRecipeCatalog catalog)
{
    internal async ValueTask<Result<LoadedInstance>> Load(InstanceId id, CancellationToken ct)
    {
        var found = await store.Get(id, ct).ConfigureAwait(false);
        if (found.IsError || found.Value is null)
        {
            return found.IsError ? found.Error : SindriErrors.InstanceUnknown.With($"no workflow instance {id.Value} (fix: sindri workflow list)");
        }

        return await WithRecipe(found.Value, ct).ConfigureAwait(false);
    }

    internal async ValueTask<Result<LoadedInstance>> WithRecipe(WorkflowInstance instance, CancellationToken ct)
    {
        var recipe = await catalog.Get(instance.Recipe, ct).ConfigureAwait(false);
        if (recipe.IsError)
        {
            return recipe.Error;
        }

        return recipe.Value.Hash == instance.RecipeHash
            ? new LoadedInstance(instance, recipe.Value)
            : SindriErrors.RecipeChanged.With(
                $"{instance.Id.Value} runs {instance.Recipe.Value} at {instance.RecipeHash.Abbreviated}, but the recipe is now {recipe.Value.Hash.Abbreviated}; cancel it and start a new attempt");
    }
}
