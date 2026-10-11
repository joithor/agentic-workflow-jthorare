using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Infrastructure.Recipes;

/// <summary>A recipe file read through the schema: the recipe and its hash, or the schema problem.</summary>
/// <param name="Recipe">The recipe, when it fits the schema.</param>
/// <param name="Hash">Its content hash.</param>
/// <param name="Problem">The schema problem, when it doesn't.</param>
internal sealed record RecipeRead(Recipe? Recipe, RecipeHash? Hash, RecipeProblem? Problem);
