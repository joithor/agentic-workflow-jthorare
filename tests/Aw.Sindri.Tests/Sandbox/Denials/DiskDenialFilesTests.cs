using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>The denial counters on disk: a launch's stored plan names its run area, and 'denials.json' sits in it.</summary>
public sealed class DiskDenialFilesTests : IDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly DateTimeOffset _at = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private DiskDenialFiles Files() => new(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _json);

    private void Launch(string launch, string area, DenialFile? counter)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "launches"));
        _ = Directory.CreateDirectory(area);
        var plan = SandboxPlannerTests.Build(Req.WriteScope) with
        {
            Environment = [new PlanEnvVar("AW_SANDBOX_PLAN", Path.Combine(area, "sandbox-plan.json"), "test")],
        };
        File.WriteAllBytes(Path.Combine(_home.Path, "launches", launch + ".sandbox.json"), AwJson.Serialize(plan, _json));
        if (counter is not null)
        {
            File.WriteAllBytes(Path.Combine(area, "denials.json"), AwJson.Serialize(counter, _json));
        }
    }

    private static DenialFile Counter(string launch, DateTimeOffset updated, string host) =>
        new(LaunchId.From(launch), updated, [new DenialEntry(DenialKind.Network, host, DenialRules.NoGroup, 3, _at, updated)], []);

    [Fact, Trait("Lane", "integration")]
    public async Task Every_launch_with_a_counter_is_listed_newest_first()
    {
        Launch(DenialsRendererTests.LaunchOne, Path.Combine(_home.Path, "areas", "one"), Counter(DenialsRendererTests.LaunchOne, _at, "one.example"));
        Launch(DenialsRendererTests.LaunchTwo, Path.Combine(_home.Path, "areas", "two"), Counter(DenialsRendererTests.LaunchTwo, _at.AddHours(1), "two.example"));

        var files = await Files().List(launch: null, Token);

        Assert.Equal([DenialsRendererTests.LaunchTwo, DenialsRendererTests.LaunchOne], files.Select(static f => f.LaunchId.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task One_launch_can_be_asked_for()
    {
        Launch(DenialsRendererTests.LaunchOne, Path.Combine(_home.Path, "areas", "one"), Counter(DenialsRendererTests.LaunchOne, _at, "one.example"));
        Launch(DenialsRendererTests.LaunchTwo, Path.Combine(_home.Path, "areas", "two"), Counter(DenialsRendererTests.LaunchTwo, _at, "two.example"));

        var files = await Files().List(LaunchId.From(DenialsRendererTests.LaunchTwo), Token);

        Assert.Equal("two.example", Assert.Single(Assert.Single(files).Entries).Target);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_launch_without_a_counter_or_with_a_broken_one_is_skipped()
    {
        Launch(DenialsRendererTests.LaunchOne, Path.Combine(_home.Path, "areas", "one"), counter: null);
        Launch(DenialsRendererTests.LaunchTwo, Path.Combine(_home.Path, "areas", "two"), counter: null);
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "areas", "two", "denials.json"), "{ not json", Token);

        var files = await Files().List(launch: null, Token);

        Assert.Empty(files);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_launches_directory_is_an_empty_list()
    {
        var files = await Files().List(launch: null, Token);

        Assert.Empty(files);
    }
}
