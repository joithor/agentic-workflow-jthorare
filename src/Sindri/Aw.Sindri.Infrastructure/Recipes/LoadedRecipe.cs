using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Infrastructure.Recipes;

/// <summary>One recipe source as the catalog read it: its catalog entry, and the recipe when it read and validated.</summary>
/// <param name="Entry">The entry (with its problems).</param>
/// <param name="Recipe">The recipe, when valid.</param>
internal sealed record LoadedRecipe(CatalogEntry Entry, ValidatedRecipe? Recipe);
