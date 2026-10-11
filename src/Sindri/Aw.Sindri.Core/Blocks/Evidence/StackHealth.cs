namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>'ui-evidence doctor''s document.</summary>
/// <param name="Healthy">Whether every check passed.</param>
/// <param name="Failures">One line per failed check.</param>
public sealed record StackHealth(bool Healthy, IReadOnlyList<string> Failures);
