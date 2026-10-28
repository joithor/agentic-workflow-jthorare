namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The commit a phase worktree was seeded at (sandbox spec §3.8), read from the stored 'fact:lineContext' the seed Step used.</summary>
internal static class PhaseSeed
{
    /// <summary>The line's base commit: a full 40-character lowercase sha in 'base'.</summary>
    /// <param name="lineContext">The fact's JSON text.</param>
    /// <returns>The sha; null when the text is not a line context with a valid base.</returns>
    internal static string? BaseOf(string lineContext) => string.IsNullOrEmpty(lineContext) ? null : null;
}
