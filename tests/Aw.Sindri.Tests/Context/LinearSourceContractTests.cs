using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

/// <summary>The real Linear adapter, over an in-memory GraphQL endpoint, passes the source contract.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearSourceContractTests : ContextSourceContract, IDisposable
{
    private readonly List<HttpClient> _clients = [];

    public void Dispose() => _clients.ForEach(c => c.Dispose());

    protected override Task<IContextSource> Create(IReadOnlyList<SeedDocument> documents)
    {
        var client = new HttpClient(new FakeLinearHandler(documents, SliceHarness.Json));
        _clients.Add(client);
        return Task.FromResult<IContextSource>(new LinearSource(client, LinearSource.Api, "lin_api_test", new LinearContext(), SliceHarness.Json));
    }
}
