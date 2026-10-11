using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

[Lane(TestLane.Integration)]
public sealed class SourceCatalogContractTests : SourceCatalogContract, IDisposable
{
    private readonly TempTree _tree = new();
    private readonly LinearHttp _linear = new();

    public void Dispose()
    {
        _linear.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public void Linear_and_connectai_say_why_they_are_skipped_and_code_and_notes_open()
    {
        var openings = Create().Open(ProfileNamingEverySource());

        Assert.Equal([SourceKind.Code, SourceKind.Notes], openings.Where(o => o.Source is not null).Select(o => o.Kind));
        Assert.Contains("LINEAR_API_KEY is not set", openings.Single(o => o.Kind == SourceKind.Linear).Skipped, StringComparison.Ordinal);
        Assert.Contains("isn't wired", openings.Single(o => o.Kind == SourceKind.ConnectAi).Skipped, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_notes_directory_is_skipped_with_the_fix()
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Context: new ContextSettings(Notes: new NotesContext("/nonexistent/notes")))).Value;

        var notes = Assert.Single(Create().Open(profile));

        Assert.StartsWith(SindriErrors.ContextSourceUnusable.Code, notes.Skipped, StringComparison.Ordinal);
        Assert.Contains("fix:", notes.Skipped, StringComparison.Ordinal);
    }

    protected override ISourceCatalog Create() =>
        new SourceCatalog(new SindriSettings(_tree.Path, Path.Combine(_tree.Path, "profile.yaml"), LinearKey: null, UserHome: _tree.Path), _linear, SliceHarness.Json);

    protected override SindriProfile ProfileNamingEverySource()
    {
        _ = Directory.CreateDirectory(Path.Combine(_tree.Path, "notes"));
        return ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("aw", _tree.Path)], Context: new ContextSettings(
            new CodeContext(), new NotesContext("~/notes"), new LinearContext(), new McpContext("connectai", "search")))).Value;
    }
}
