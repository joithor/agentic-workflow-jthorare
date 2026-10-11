using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>An instance with the recipe it is pinned to.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Recipe">Its recipe, at the hash it started with.</param>
public sealed record LoadedInstance(WorkflowInstance Instance, ValidatedRecipe Recipe);
