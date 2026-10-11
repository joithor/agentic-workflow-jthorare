using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>A registered recipe that passed validation: what the engine runs.</summary>
/// <param name="Recipe">The recipe.</param>
/// <param name="Hash">Its content hash; an instance is pinned to it.</param>
/// <param name="Approved">Whether it is approved (built in) rather than a recipe file.</param>
/// <param name="Source">Where it came from: 'builtin:&lt;name&gt;' or the file path.</param>
public sealed record ValidatedRecipe(Recipe Recipe, RecipeHash Hash, bool Approved, string Source);
