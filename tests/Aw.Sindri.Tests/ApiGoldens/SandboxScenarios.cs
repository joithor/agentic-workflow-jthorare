using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// SB-6a's goldens: 'GET /sandbox/denials?item=' over an item's launch (and one launch of no item), and 'POST /sandbox/allow' for the host that can be allowed, the path that never can,
/// a tool, and a browser page. The counters are fixed (<see cref="FixedDenials"/>); the launch and the item are appended to the ledger as the launcher would.
/// </summary>
internal static class SandboxScenarios
{
    private const string Owned = "LCH-00000000000000000000000000000001";
    private const string Unowned = "LCH-00000000000000000000000000000002";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sandbox-denials", "by-item"), ("sandbox-denials", "other-item"),
        ("sandbox-allow", "needs-approval"), ("sandbox-allow", "refused-never"), ("sandbox-allow", "refused-policy"), ("sandbox-allow", "refused-origin"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item scenarios' profile, with the fixed counters in place of the run areas'.</summary>
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services => services.Replace(ServiceDescriptor.Singleton<IDenialFiles>(new FixedDenials())));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        var item = await ItemScenarios.Create(host, "Try the sandbox", "demo");
        await LaunchScenarios.Started(host, LaunchId.From(Owned), item, ViewerOutcomeKind.Skipped, ct);
        await ViewScenarios.CatchUp(host, ct);
        var human = ItemScenarios.Human(host);
        return slug == "sandbox-denials"
            ? (await host.Http.Ask(Get(scenario == "by-item" ? $"/sindri/sandbox/denials?item={item}" : "/sindri/sandbox/denials?item=ITM-00000000000000000000000000000fff", human))).Body
            : (await host.Http.Ask(Post("/sindri/sandbox/allow", Allow(scenario), scenario == "refused-origin" ? [.. human, ("Origin", "http://localhost:3000")] : human))).Body;
    }

    private static string Allow(string scenario) => scenario switch
    {
        "refused-never" => $$"""{"launch":"{{Owned}}","kind":"path","target":"/var/run/docker.sock"}""",
        "refused-policy" => $$"""{"launch":"{{Owned}}","kind":"policy","target":"git push"}""",
        _ => $$"""{"launch":"{{Owned}}","kind":"network","target":"github.com"}""",
    };

    /// <summary>The counters of two launches: the first is the item's, the second no item's.</summary>
    private sealed class FixedDenials : IDenialFiles
    {
        private static readonly DateTimeOffset _at = KnownInstants.Epoch;

        public ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken)
        {
            IReadOnlyList<DenialFile> all =
            [
                new(
                    LaunchId.From(Owned), _at,
                    [
                        new(DenialKind.Network, "github.com", DenialRules.NoGroup, 3, _at, _at),
                        new(DenialKind.Path, "/var/run/docker.sock", DenialRules.DenyList, 1, _at, _at),
                        new(DenialKind.Policy, "git push", DenialRules.InnerPolicy, 1, _at, _at),
                    ], []),
                new(LaunchId.From(Unowned), _at, [new(DenialKind.Network, "example.org", DenialRules.NoGroup, 1, _at, _at)], []),
            ];
            return ValueTask.FromResult<IReadOnlyList<DenialFile>>([.. all.Where(f => launch is not { } one || f.LaunchId == one)]);
        }
    }
}
