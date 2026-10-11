using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory recipe catalog: recipes added in code, validated like the file catalog validates; passes <see cref="RecipeCatalogContract"/>.</summary>
/// <param name="blocks">The registry recipes are validated against.</param>
public sealed class InMemoryRecipeCatalog(IBlockRegistry blocks) : IRecipeCatalog
{
    private readonly List<(Recipe Recipe, RecipeHash Hash, bool Approved)> _recipes = [];

    /// <summary>Adds (or replaces, by id) a recipe.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="hash">Its hash.</param>
    /// <param name="approved">Whether it is approved.</param>
    public void Add(Recipe recipe, RecipeHash hash, bool approved = true)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        _ = _recipes.RemoveAll(r => r.Recipe.Id == recipe.Id);
        _recipes.Add((recipe, hash, approved));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<CatalogEntry>>> List(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<CatalogEntry>>>(_recipes.Select(r => Entry(r.Recipe, r.Hash, r.Approved)).ToList());

    /// <inheritdoc/>
    public ValueTask<Result<ValidatedRecipe>> Get(RecipeId id, CancellationToken cancellationToken)
    {
        var found = _recipes.FindIndex(r => r.Recipe.Id == id);
        if (found < 0)
        {
            return ValueTask.FromResult<Result<ValidatedRecipe>>(SindriErrors.RecipeUnknown.With($"no recipe {id.Value}"));
        }

        var (recipe, hash, approved) = _recipes[found];
        var entry = Entry(recipe, hash, approved);
        return ValueTask.FromResult<Result<ValidatedRecipe>>(entry.Valid
            ? new ValidatedRecipe(recipe, hash, approved, $"memory:{id.Value}")
            : SindriErrors.RecipeUnknown.With($"recipe {id.Value} is invalid: {string.Join("; ", entry.Problems)}"));
    }

    /// <inheritdoc/>
    public ValueTask<Result<CatalogEntry>> Inspect(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<CatalogEntry>>(SindriErrors.RecipeUnknown.With($"the in-memory catalog reads no files ({path})"));

    private CatalogEntry Entry(Recipe recipe, RecipeHash hash, bool approved)
    {
        var known = _recipes.Select(r => r.Recipe.Id).ToHashSet();
        return new CatalogEntry($"memory:{recipe.Id.Value}", recipe.Id, recipe.Version, hash, approved, RecipeValidator.Validate(recipe, hash, approved, blocks, known));
    }
}
