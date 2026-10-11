using Aw.Sindri.Core.Blocks;

namespace Aw.Sindri.Features.ListBlocks;

/// <summary>The blocks.</summary>
/// <param name="Blocks">Each block's declaration, ordered by id.</param>
public sealed record ListBlocksResponse(IReadOnlyList<BlockSpec> Blocks);
