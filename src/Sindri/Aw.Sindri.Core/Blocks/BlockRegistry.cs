using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>
/// The registry over every <see cref="IBlock"/> in the container. A broken registration is a bug, so it throws at
/// construction: two blocks with one id, a gate that isn't an <see cref="IGateBlock"/> or has no rung, an effect that
/// isn't an <see cref="IEffectBlock"/> or has no command kind.
/// </summary>
public sealed class BlockRegistry : IBlockRegistry
{
    private readonly Dictionary<BlockId, IBlock> _blocks = [];

    /// <summary>Registers <paramref name="blocks"/>.</summary>
    /// <param name="blocks">Every registered block.</param>
    public BlockRegistry(IEnumerable<IBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        foreach (var block in blocks)
        {
            var problem = Problem(block);
            if (problem is not null)
            {
                throw new InvalidOperationException($"Block {block.Spec.Id.Value}: {problem}");
            }

            if (!_blocks.TryAdd(block.Spec.Id, block))
            {
                throw new InvalidOperationException($"Block {block.Spec.Id.Value} is registered twice");
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<BlockSpec> List() => [.. _blocks.Values.Select(b => b.Spec).OrderBy(s => s.Id.Value, StringComparer.Ordinal)];

    /// <inheritdoc/>
    public BlockSpec? Find(BlockId id) => _blocks.TryGetValue(id, out var block) ? block.Spec : null;

    /// <inheritdoc/>
    public IGateBlock? Gate(BlockId id) => _blocks.TryGetValue(id, out var block) ? block as IGateBlock : null;

    /// <inheritdoc/>
    public IEffectBlock? Effect(BlockId id) => _blocks.TryGetValue(id, out var block) ? block as IEffectBlock : null;

    private static string? Problem(IBlock block)
    {
        var spec = block.Spec;
        if (spec.Kind is BlockKind.Gate)
        {
            return block is IGateBlock && spec.Rung is not null ? null : "a gate implements IGateBlock and declares its rung";
        }

        if (spec.Rung is not null)
        {
            return "only a gate has a rung";
        }

        if (spec.Kind is BlockKind.Effect)
        {
            return block is IEffectBlock && !string.IsNullOrWhiteSpace(spec.CommandKind) ? null : "an effect implements IEffectBlock and declares its command kind";
        }

        return spec.Kind is BlockKind.Evidence && !spec.Id.Value.StartsWith("evidence.", StringComparison.Ordinal) ? "an evidence kind's id is 'evidence.<kind>'" : null;
    }
}
