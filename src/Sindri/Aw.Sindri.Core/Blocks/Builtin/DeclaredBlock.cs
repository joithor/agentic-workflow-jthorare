namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>A block that is only a declaration: a Step implementation that runs outside the engine and reports its outputs through a signal, or an evidence kind.</summary>
/// <param name="spec">The declaration.</param>
public sealed class DeclaredBlock(BlockSpec spec) : IBlock
{
    /// <inheritdoc/>
    public BlockSpec Spec { get; } = spec;
}
