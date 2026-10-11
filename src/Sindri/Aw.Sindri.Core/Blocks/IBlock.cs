namespace Aw.Sindri.Core.Blocks;

/// <summary>
/// A building block (factory addendum §17.28.2): a typed C# module registered once (as IBlock, in its project's Add…()
/// method) and reused by every recipe that names its id. Gates implement <see cref="IGateBlock"/>, effects
/// <see cref="IEffectBlock"/>; steps and evidence kinds are declarations the engine checks recipes against.
/// </summary>
public interface IBlock
{
    /// <summary>The block's declaration.</summary>
    public BlockSpec Spec { get; }
}
