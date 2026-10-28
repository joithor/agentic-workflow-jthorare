using System.Globalization;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The box facts of a Linux launch's sandbox step: paths from the launcher's own options, memory and cores from the machine port, the control plane's port, the catalog's policy form.</summary>
/// <param name="Launcher">The launcher's files.</param>
/// <param name="Catalog">The agent catalog.</param>
/// <param name="Machine">The box's memory and cores (the same numbers the doctor shows).</param>
/// <param name="Processes">Runs git.</param>
/// <param name="Environment">The process environment (HOME, AW_STATE_DIR, PATH ...), read at each launch.</param>
/// <param name="Control">The running control plane's endpoint record, or null when unknown.</param>
/// <param name="Counters">What a launch's denial counter is made from, or null to count no denials.</param>
#pragma warning disable MA0182 // registered by the product Host in SB-5a's switch step
internal sealed class LinuxSandboxLaunchInputs(LauncherOptions Launcher, IAgentCatalog Catalog, ISandboxMachine Machine, ISandboxProcesses Processes, Func<IReadOnlyDictionary<string, string>> Environment, Func<ControlEndpoint?> Control, DenialCounterParts? Counters = null) : ISandboxLaunchInputs
{
    /// <summary>The proxy port inside every sandbox (its network namespace is private, so one fixed port can't clash).</summary>
    internal const int FixedProxyPort = 3128;

    private static readonly TimeSpan _gitTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public SandboxHostFacts Host()
    {
        var env = Environment();
        var home = env.GetValueOrDefault("HOME") ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var state = env.GetValueOrDefault("AW_STATE_DIR") is { Length: > 0 } set ? set : Path.Combine(home, ".agentic-workflow");
        return new SandboxHostFacts(home, state, Launcher.SindriHome ?? Launcher.Home, CsBinDir(state), Uid(), Machine.RamBytes, Machine.Cores, IsMacOs: false, env, OtherHomes("/home", home));
    }

    /// <inheritdoc/>
    public ValueTask<string> GitCommonDir(string repoPath, CancellationToken cancellationToken)
    {
        var ran = Processes.Run("git", ["-C", repoPath, "rev-parse", "--git-common-dir"], _gitTimeout);
        var printed = ran.Succeeded ? ran.Output.Trim() : string.Empty;
        return ValueTask.FromResult(printed.Length == 0 ? Path.Combine(repoPath, ".git") : Path.GetFullPath(printed, repoPath));
    }

    /// <inheritdoc/>
    public string RunAreaFor(LaunchId launch) => Path.Combine(Launcher.LaunchDirectory, "areas", launch.Value);

    /// <inheritdoc/>
    public string NetDirFor(LaunchId launch)
    {
        var runtime = string.Create(CultureInfo.InvariantCulture, $"/run/user/{Uid()}");
        return NetDir(System.IO.Directory.Exists(runtime) ? runtime : "/tmp", launch);
    }

    /// <inheritdoc/>
    public int SindriPort() => Control()?.Url.Port ?? 0;

    /// <inheritdoc/>
    public int ProxyPort() => FixedProxyPort;

    /// <inheritdoc/>
    public string PolicyDirectory() => Launcher.PolicyDirectory;

    /// <inheritdoc/>
    public PolicyForm PolicyFormOf(AgentId agent) => Catalog.Resolve(agent)?.PolicyForm ?? PolicyForm.None;

    /// <inheritdoc/>
    public DenialCounter? DenialCounterFor(LaunchId launch) =>
        Counters is { } parts
            ? new DenialCounter(launch, Path.Combine(Launcher.LaunchDirectory, "denials", launch.Value + ".json"), parts.Json, parts.Scrubber, parts.Clock) { Sink = parts.Sink }
            : null;

    /// <summary>The socket directory of a launch: short, because a unix socket path holds about 100 characters (<c lang="text">&lt;base&gt;/aw-net/&lt;12 hex&gt;</c>).</summary>
    /// <param name="baseDirectory">/run/user/&lt;uid&gt; or /tmp.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The path.</returns>
    internal static string NetDir(string baseDirectory, LaunchId launch) =>
        Path.Combine(baseDirectory, "aw-net", launch.Value.Replace("LCH-", string.Empty, StringComparison.Ordinal)[..12]);

    /// <summary>The other users' home directories under <paramref name="root"/>, which the plan denies.</summary>
    /// <param name="root">The parent of the homes (/home).</param>
    /// <param name="ownHome">The owner's home.</param>
    /// <returns>The paths, sorted.</returns>
    internal static IReadOnlyList<string> OtherHomes(string root, string ownHome)
    {
        try
        {
            return System.IO.Directory.Exists(root)
                ? [.. System.IO.Directory.EnumerateDirectories(root).Where(d => !string.Equals(d, ownHome.TrimEnd('/'), StringComparison.Ordinal)).Order(StringComparer.Ordinal)]
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string CsBinDir(string state)
    {
        var current = Path.Combine(state, "bin", "cs", "current");
        try
        {
            return System.IO.Directory.ResolveLinkTarget(current, returnFinalTarget: true)?.FullName ?? string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;   // none installed
        }
    }

    private static int Uid()
    {
        try
        {
            var line = File.ReadLines("/proc/self/status").FirstOrDefault(l => l.StartsWith("Uid:", StringComparison.Ordinal));
            var fields = line?.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            return fields is { Length: > 1 } && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var uid) ? uid : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
#pragma warning restore MA0182
