using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Loads an instance and its recipe, refusing one whose recipe changed under it (§17.28.1: a recipe is versioned and
/// hashed; an instance keeps the one it started with). The human's commands (cancel, park, take over, hand back,
/// evidence) don't need the recipe, so a stranded instance can still be cancelled.
/// </summary>
/// <param name="store">The workflow store.</param>
/// <param name="catalog">The recipe catalog.</param>
internal sealed class InstanceLoader(IWorkflowStore store, IRecipeCatalog catalog)
{
    internal async ValueTask<Result<LoadedInstance>> Load(InstanceId id, CancellationToken ct)
    {
        var found = await Find(id, ct).ConfigureAwait(false);
        return found.IsError ? found.Error : await WithRecipe(found.Value, ct).ConfigureAwait(false);
    }

    internal async ValueTask<Result<WorkflowInstance>> Find(InstanceId id, CancellationToken ct)
    {
        var found = await store.Get(id, ct).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value is { } instance ? instance : SindriErrors.InstanceUnknown.With($"no workflow instance {id.Value} (fix: sindri workflow list)");
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

    /// <summary>The instance's recipe, or null when it is stranded (its recipe changed or is gone); any other failure is an error.</summary>
    internal async ValueTask<Result<ValidatedRecipe?>> Optional(WorkflowInstance instance, CancellationToken ct)
    {
        var loaded = await WithRecipe(instance, ct).ConfigureAwait(false);
        if (loaded.IsSuccess)
        {
            return loaded.Value.Recipe;
        }

        return Stranded(loaded.Error) ? Results.Success<ValidatedRecipe?>(value: null) : loaded.Error;
    }

    /// <summary>True for the errors that mean the instance's recipe changed or is gone, so it can't move on its own.</summary>
    internal static bool Stranded(Error error) => error.Code == SindriErrors.RecipeChanged.Code || error.Code == SindriErrors.RecipeUnknown.Code;
}
