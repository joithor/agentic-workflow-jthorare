using Aw.Kernel.Testing;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Recipes;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Integration)]
public sealed class FileRecipeCatalogContractTests : RecipeCatalogContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ValueTask<IRecipeCatalog> WithBuiltins() =>
        ValueTask.FromResult<IRecipeCatalog>(new FileRecipeCatalog(new SindriSettings(_home.Path, Path.Combine(_home.Path, "p.yaml")), Registries.Builtin(), SliceHarness.Json));
}
