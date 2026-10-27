namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The Steps that must differ from an earlier Step of their instance (#1008). Scoping's second opinion is the only one; the general rule a recipe declares is #1269.</summary>
internal static class ChallengerSteps
{
    /// <summary>The state whose Step <paramref name="block"/> must differ from.</summary>
    /// <param name="block">The Step's block id.</param>
    /// <returns>The drafter's state id, or null when the Step is no challenger.</returns>
    public static string? DrafterState(string block) => string.Equals(block, "step.scope.challenge", StringComparison.Ordinal) ? "draft" : null;
}
