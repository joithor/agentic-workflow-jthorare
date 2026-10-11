using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>One recipe the catalog found, valid or not: what 'sindri workflow recipes' and 'validate' show.</summary>
/// <param name="Source">Where it came from: 'builtin:&lt;name&gt;' or the file path.</param>
/// <param name="Id">Its id, when the file could be read that far.</param>
/// <param name="Version">Its version, when read.</param>
/// <param name="Hash">Its content hash, when read.</param>
/// <param name="Approved">Whether it is approved (built in).</param>
/// <param name="Problems">Every validation problem; none means it is runnable.</param>
public sealed record CatalogEntry(string Source, RecipeId? Id, int? Version, RecipeHash? Hash, bool Approved, IReadOnlyList<RecipeProblem> Problems)
{
    /// <summary>Whether the recipe can run.</summary>
    public bool Valid => Problems.Count == 0;
}
