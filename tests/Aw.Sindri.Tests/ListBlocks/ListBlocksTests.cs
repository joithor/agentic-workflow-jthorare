using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ListBlocks;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ListBlocks;

public sealed class ListBlocksTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListBlocks_lists_the_builtin_and_registered_blocks_with_their_kind()
    {
        await using var h = new WorkflowHarness();

        var blocks = (await new ListBlocksService(h.Blocks).Handle(new ListBlocksRequest(), WorkflowHarness.Token)).Value.Blocks;

        Assert.Contains(blocks, b => b.Id == BlockId.From("gate.checks.passed") && b.Kind == BlockKind.Gate);
        Assert.Contains(blocks, b => b.Id == BlockId.From("effect.notify.human") && b.Kind == BlockKind.Effect);
        Assert.Contains(blocks, b => b.Id == BlockId.From("gate.test.model"));
    }
}
