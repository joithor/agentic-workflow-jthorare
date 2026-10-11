namespace Aw.Sindri.Core.Recipes;

/// <summary>One validation problem: the check that found it (<see cref="RecipeChecks"/>), where, and what to fix.</summary>
/// <param name="Check">The check, for example 'reachable'.</param>
/// <param name="Path">Where in the recipe, for example 'states.review'.</param>
/// <param name="Message">What is wrong and what is expected.</param>
public sealed record RecipeProblem(string Check, string Path, string Message)
{
    /// <summary>The problem as one line: 'check path: message'.</summary>
    /// <returns>The line.</returns>
    public override string ToString() => $"{Check} {Path}: {Message}";
}
