using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

[Lane(TestLane.Unit)]
public sealed class FakeContextSourceContractTests : ContextSourceContract
{
    protected override Task<IContextSource> Create(IReadOnlyList<SeedDocument> documents) =>
        Task.FromResult<IContextSource>(new FakeContextSource(SourceKind.Notes, documents));
}
