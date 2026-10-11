using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>
/// The recipe catalog (port): the built-in recipes (approved) and the recipe files in Sindri's home (unapproved), each
/// validated against the block registry. A changed file is a changed recipe: a new hash.
/// </summary>
public interface IRecipeCatalog
{
    /// <summary>Every recipe found, valid or not, ordered by source.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entries.</returns>
    public ValueTask<Result<IReadOnlyList<CatalogEntry>>> List(CancellationToken cancellationToken);

    /// <summary>The valid recipe <paramref name="id"/>.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The recipe, or SND-WF-002 when none is registered with that id or it is invalid.</returns>
    public ValueTask<Result<ValidatedRecipe>> Get(RecipeId id, CancellationToken cancellationToken);

    /// <summary>Reads and validates the recipe file at <paramref name="path"/> as an unapproved recipe, without registering it.</summary>
    /// <param name="path">The YAML file.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entry with its problems, or SND-WF-002 when the file can't be read.</returns>
    public ValueTask<Result<CatalogEntry>> Inspect(string path, CancellationToken cancellationToken);
}
