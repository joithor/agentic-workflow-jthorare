namespace Aw.Sindri.Features.ValidateRecipe;

/// <summary>Validate a recipe file as an unapproved recipe (factory addendum §17.28.5), without registering it.</summary>
/// <param name="Path">The YAML file.</param>
public sealed record ValidateRecipeRequest(string Path);
