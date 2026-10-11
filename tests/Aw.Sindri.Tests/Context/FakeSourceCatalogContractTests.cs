using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

[Lane(TestLane.Unit)]
public sealed class FakeSourceCatalogContractTests : SourceCatalogContract
{
    protected override ISourceCatalog Create() => new FakeSourceCatalog().With(new FakeContextSource(SourceKind.Notes, []));

    protected override SindriProfile ProfileNamingEverySource() =>
        ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("aw", "/repos/aw")], Context: new ContextSettings(
            new CodeContext(), new NotesContext("/notes"), new LinearContext(), new McpContext("connectai", "search")))).Value;
}
