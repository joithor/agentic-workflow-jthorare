using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.QueryContext;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Context;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.QueryContext;

/// <summary>The in-memory ports a QueryContext test needs: approvals, a catalog of fake sources, the fake scrubber and a clock.</summary>
internal sealed class ContextHarness
{
    public static readonly IReadOnlyList<SeedDocument> Code =
    [
        new("src/Lease/SqliteLease.cs", "SqliteLease.cs", "// The host lease, fenced by its epoch.\nclass SqliteLease { long Epoch; }"),
        new("src/Ledger/SqliteLedger.cs", "SqliteLedger.cs", "// Appends check the lease epoch."),
        new("src/Garden.cs", "Garden.cs", "// tomatoes"),
    ];

    public static readonly IReadOnlyList<SeedDocument> Notes =
    [
        new("notes/lease.md", "lease.md", "# Lease\nThe host lease holds an epoch for fencing."),
        new("notes/deploy.md", "deploy.md", $"# Deploy\nThe deploy token is {TextScrubberContract.PlantedToken} for the lease host."),
    ];

    public ContextHarness()
    {
        CodeSource = new FakeContextSource(SourceKind.Code, Code);
        NotesSource = new FakeContextSource(SourceKind.Notes, Notes);
        Catalog = new FakeSourceCatalog().With(CodeSource).With(NotesSource);
    }

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public InMemoryProfiles Profiles { get; } = new();

    public FakeContextSource CodeSource { get; }

    public FakeContextSource NotesSource { get; }

    public FakeSourceCatalog Catalog { get; }

    public QueryContextService Service { get; } = new();

    /// <summary>Approves a profile whose context section is <paramref name="context"/> (null: none).</summary>
    public async Task Approve(ContextSettings? context)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("aw", "/repos/aw")], Context: context)).Value;
        var loaded = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
        _ = await Profiles.Save(loaded, TestContext.Current.CancellationToken);
    }

    /// <summary>A context section with code and notes on.</summary>
    public static ContextSettings CodeAndNotes(int limit = ContextSettings.DefaultLimit) =>
        new(Code: new CodeContext(), Notes: new NotesContext("/notes"), Limit: limit);
}
