using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Recipes;

/// <summary>
/// The built-in registry plus blocks that BlockRegistry would refuse (an effect off the allowlist), so the validator's
/// own checks can be shown to catch what another registry lets through.
/// </summary>
internal sealed class UncheckedRegistry(IBlockRegistry inner, params IBlock[] extra) : IBlockRegistry
{
    public IReadOnlyList<BlockSpec> List() => [.. inner.List(), .. extra.Select(b => b.Spec)];

    public BlockSpec? Find(BlockId id) => Extra(id)?.Spec ?? inner.Find(id);

    public IGateBlock? Gate(BlockId id) => Extra(id) as IGateBlock ?? inner.Gate(id);

    public IEffectBlock? Effect(BlockId id) => Extra(id) as IEffectBlock ?? inner.Effect(id);

    private IBlock? Extra(BlockId id) => extra.FirstOrDefault(b => b.Spec.Id == id);
}
