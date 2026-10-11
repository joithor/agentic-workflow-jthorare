using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Blocks;

namespace Aw.Sindri.Features.ListBlocks;

internal sealed class ListBlocksService(IBlockRegistry blocks) : IService<ListBlocksRequest, ListBlocksResponse>
{
    public ValueTask<Result<ListBlocksResponse>> Handle(ListBlocksRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ListBlocksResponse>>(new ListBlocksResponse(blocks.List()));
}
