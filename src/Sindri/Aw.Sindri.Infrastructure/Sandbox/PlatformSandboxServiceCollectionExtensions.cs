using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Infrastructure.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The one switch that turns the sandbox on (sandbox spec §12 SB-5a, SB12): on Linux the bubblewrap sandbox, its owner-copy login source and the concurrent-session cap
/// replace the 'UnavailableSandbox' Core registers; everywhere else nothing changes and every sandboxed launch is refused with SND-LAUNCH-014.
/// </summary>
public static class PlatformSandboxServiceCollectionExtensions
{
    /// <summary>The slice every Step's systemd scope lives in, below the user manager's cgroup directory (the cap counts '*.scope' directories there).</summary>
    private const string SliceCgroupPath = "sindri.slice/sindri-steps.slice";

    /// <summary>Registers the product's sandbox for this platform.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriPlatformSandbox(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSindriPlatformSandbox(environment, OperatingSystem.IsLinux(), OwnUid.Read());

    internal static IServiceCollection AddSindriPlatformSandbox(this IServiceCollection services, Func<string, string?> environment, bool linux, int uid)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        if (!linux)
        {
            return services;
        }

        _ = services.RemoveAll<ISandbox>();
        return services
            .AddSindriBubblewrapSandbox(environment)
            .AddSingleton<ISliceScopes>(new CgroupSliceScopes(SliceDirectory(uid)))
            .AddSingleton<ISessionCap>(sp => new FileSessionCap(LockFile(sp.GetRequiredService<SindriSettings>().Home), sp.GetRequiredService<ISliceScopes>()))
            .AddSingleton<ISandboxLaunchStages, PendingSandboxLaunchStages>()   // TODO(flow): replace with the flow builder's ISandboxLaunchStages implementation (the one line that remains)
            .AddSingleton<ISandboxLaunch>(sp => new SandboxLaunch(
                sp.GetRequiredService<ISandbox>(), sp.GetRequiredService<ISessionCap>(), sp.GetRequiredService<ISandboxLaunchStages>(), LaunchOptions(sp, environment)));
    }

    private static SandboxLaunchOptions LaunchOptions(IServiceProvider sp, Func<string, string?> environment)
    {
        var endpoints = sp.GetRequiredService<IEndpointFile>();
        return new SandboxLaunchOptions(
            environment("AW_SANDBOX_SYSTEMD_RUN") is { Length: > 0 } run ? run : "systemd-run",
            ReadBox(),
            TimeSpan.FromHours(2),   // workflow.steps.longMax's default; the launch reads no profile here yet
            sp.GetRequiredService<SindriSettings>().Home,
            sp.GetRequiredService<LauncherOptions>().SecretsDirectory,
            () => endpoints.Read(CancellationToken.None).AsTask().GetAwaiter().GetResult() is { IsSuccess: true } read ? read.Value : null);   // control.json is a tiny local file
    }

    /// <summary>Reads the box's RAM (MemTotal in /proc/meminfo, in kB) and logical cores.</summary>
    /// <returns>The facts; 0 bytes of RAM when /proc/meminfo can't be read.</returns>
    internal static BoxFacts ReadBox()
    {
        long ram = 0;
        try
        {
            var line = File.ReadLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemTotal:", StringComparison.Ordinal));
            var kb = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            ram = long.TryParse(kb, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed * 1024 : 0;
        }
        catch (IOException)
        {
            // No /proc: the planner treats 0 bytes as unknown.
        }

        return new BoxFacts(ram, Environment.ProcessorCount);
    }

    /// <summary>Gets the cgroup directory whose '*.scope' children are the live Step sessions.</summary>
    /// <param name="uid">The real uid.</param>
    /// <returns>The directory.</returns>
    internal static string SliceDirectory(int uid)
    {
        var id = uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"/sys/fs/cgroup/user.slice/user-{id}.slice/user@{id}.service/{SliceCgroupPath}";
    }

    /// <summary>Gets the lock file the session cap takes, under Sindri's real home.</summary>
    /// <param name="sindriHome">$AW_SINDRI_HOME.</param>
    /// <returns>'&lt;home&gt;/locks/sessions.lock'.</returns>
    internal static string LockFile(string sindriHome) => Path.Combine(sindriHome, "locks", "sessions.lock");
}
