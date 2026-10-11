using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>The block registry: every block this build registered, by id, with its declaration (factory addendum §17.28.2).</summary>
public interface IBlockRegistry
{
    /// <summary>Every block's declaration, ordered by id.</summary>
    /// <returns>The declarations.</returns>
    public IReadOnlyList<BlockSpec> List();

    /// <summary>The declaration of <paramref name="id"/>, or null when no block has it.</summary>
    /// <param name="id">The block id.</param>
    /// <returns>The declaration.</returns>
    public BlockSpec? Find(BlockId id);

    /// <summary>The gate <paramref name="id"/>, or null when it isn't a registered gate.</summary>
    /// <param name="id">The block id.</param>
    /// <returns>The gate.</returns>
    public IGateBlock? Gate(BlockId id);

    /// <summary>The effect <paramref name="id"/>, or null when it isn't a registered effect.</summary>
    /// <param name="id">The block id.</param>
    /// <returns>The effect.</returns>
    public IEffectBlock? Effect(BlockId id);
}
