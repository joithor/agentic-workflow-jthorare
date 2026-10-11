using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>
/// Validates a recipe at registration and before a generated recipe is frozen (factory addendum §17.28.5): the schema
/// (checked by the typed read, reported by the reader), shape, registered blocks, typed inputs produced upstream,
/// capability blocks only in approved recipes, reachability, a way out of every non-terminal state, timeouts, loop caps,
/// allowlisted effects and the hash.
/// </summary>
public static class RecipeValidator
{
    /// <summary>Every problem with <paramref name="recipe"/>; none means it is valid.</summary>
    /// <param name="recipe">The recipe, as read.</param>
    /// <param name="hash">The hash computed from its content.</param>
    /// <param name="approved">Whether it is an approved recipe (built in, or approved by hash); unapproved ones can't name capability blocks.</param>
    /// <param name="blocks">The block registry.</param>
    /// <param name="knownRecipes">The recipe ids a child may name.</param>
    /// <returns>The problems, in a stable order.</returns>
    public static IReadOnlyList<RecipeProblem> Validate(Recipe recipe, RecipeHash hash, bool approved, IBlockRegistry blocks, IReadOnlySet<RecipeId> knownRecipes)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var problems = RecipeShapeChecks.Check(recipe, knownRecipes)
            .Concat(RecipeBlockChecks.Check(recipe, approved, blocks))
            .Concat(RecipeDataflow.Check(recipe, blocks))
            .Concat(RecipeGraphChecks.Check(recipe))
            .ToList();
        if (recipe.Hash is { } declared && declared != hash)
        {
            problems.Add(new(RecipeChecks.Hash, "hash", $"the recipe says {declared.Value} but its content hashes to {hash.Value}"));
        }

        return problems;
    }
}
