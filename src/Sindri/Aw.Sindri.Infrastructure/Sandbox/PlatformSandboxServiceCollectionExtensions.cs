using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
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

        services.RemoveAll<ISandbox>();
        return services
            .AddSindriBubblewrapSandbox(environment)
            .AddSingleton<ISliceScopes>(new CgroupSliceScopes(SliceDirectory(uid)))
            .AddSingleton<ISessionCap>(sp => new FileSessionCap(LockFile(sp.GetRequiredService<SindriSettings>().Home), sp.GetRequiredService<ISliceScopes>()));
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
