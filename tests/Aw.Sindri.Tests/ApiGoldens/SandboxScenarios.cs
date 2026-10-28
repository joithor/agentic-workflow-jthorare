using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Features.GetSandboxStatus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The sandbox status and check goldens (sandbox spec §8, SB-6): the real checks probe this box, so the host runs them over a fixed answer (the machine's memory, the platform's mechanism and the
/// blocker the check finds are the scenario's), recorded the way serve records any check. 'blocked' finds bubblewrap missing; 'ready' finds nothing.
/// </summary>
internal static class SandboxScenarios
{
    private const long Gib = 1024L * 1024 * 1024;

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sandbox-status", "never-checked"), ("sandbox-status", "after-blocked-check"), ("sandbox-status", "after-ready-check"),
        ("sandbox-check", "blocked"), ("sandbox-check", "ready"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host over the fixed machine, platform and check.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct)
    {
        var blocked = scenario.Contains("blocked", StringComparison.Ordinal);
        return GoldenHost.Start(temporary: true, ct, GoldenHost.ProfileYaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ISandboxMachine>(new FixedMachine()));
            _ = services.Replace(ServiceDescriptor.Singleton<ISandboxPlatformName>(new FixedPlatform()));
            _ = services.Replace(ServiceDescriptor.Scoped<ISandboxChecks>(sp => new RecordingSandboxChecks(new FixedChecks(blocked), sp.GetRequiredService<LastSandboxCheck>(), sp.GetRequiredService<TimeProvider>())));
        });
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var headers = Human(host.Token.Value);
        if (slug == "sandbox-check")
        {
            return (await host.Http.Ask(Get("/sindri/sandbox/check", headers))).Body;
        }

        if (scenario.StartsWith("after-", StringComparison.Ordinal))
        {
            _ = await host.Http.Ask(Get("/sindri/sandbox/check", headers));
        }

        return (await host.Http.Ask(Get("/sindri/sandbox/status", headers))).Body;
    }

    private sealed class FixedMachine : ISandboxMachine
    {
        public long RamBytes => 62 * Gib;

        public int Cores => 8;
    }

    private sealed class FixedPlatform : ISandboxPlatformName
    {
        public string Mechanism => "bubblewrap";
    }

    private sealed class FixedChecks(bool blocked) : ISandboxChecks
    {
        public ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken) => ValueTask.FromResult(new SandboxCheckReport(
            blocked ? [SandboxBlocker.Of(SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoBwrap))] : [], []));
    }
}
