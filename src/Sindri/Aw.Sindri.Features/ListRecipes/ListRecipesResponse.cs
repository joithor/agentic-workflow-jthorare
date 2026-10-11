using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Features.ListRecipes;

/// <summary>The recipes.</summary>
/// <param name="Recipes">Each with its source, id, version, hash, approval and problems.</param>
public sealed record ListRecipesResponse(IReadOnlyList<CatalogEntry> Recipes);
