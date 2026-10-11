using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

/// <summary>The ConnectAI source over the fake MCP tool reader passes the source contract.</summary>
[Lane(TestLane.Unit)]
public sealed class McpContextSourceContractTests : ContextSourceContract
{
    protected override Task<IContextSource> Create(IReadOnlyList<SeedDocument> documents) =>
        Task.FromResult<IContextSource>(new McpContextSource(new FakeMcpToolReader(documents), new McpContext("connectai", "search")));
}
