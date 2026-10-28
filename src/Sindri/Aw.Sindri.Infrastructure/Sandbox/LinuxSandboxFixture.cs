using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox.Launch;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The throw-away request Check again plans and self-checks (sandbox spec §8): a ReadOnly Step on a small git repository under '&lt;home&gt;/sandbox/fixture', with the box's
/// real host facts. The repository is made once; nothing of the owner's is read or written besides it.
/// </summary>
/// <param name="sindriHome">The real AW_SINDRI_HOME.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="processes">Runs git for the one 'git init'.</param>
internal sealed class LinuxSandboxFixture(string sindriHome, Func<string, string?> environment, ISandboxProcesses processes) : ISandboxFixture
{
    private static readonly TimeSpan _gitTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public SandboxRequest? Request()
    {
        var root = Path.Combine(sindriHome, "sandbox", "fixture");
        var repo = Path.Combine(root, "repo");
        var worktree = Path.Combine(repo, ".claude", "worktrees", "sindri-fixture-1");
        var runDirectory = Path.Combine(root, "run");
        var runArea = Path.Combine(root, "area");
        var netDir = Path.Combine(NetParent(environment), "fixture");
        try
        {
            foreach (var directory in new[] { worktree, runDirectory, runArea, netDir })
            {
                _ = Directory.CreateDirectory(directory);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // no fixture on this box: the self-check is skipped, not failed
        }

        if (!Directory.Exists(Path.Combine(repo, ".git")) && !processes.Run("git", ["-C", repo, "init", "-q"], _gitTimeout).Succeeded)
        {
            return null;
        }

        return Build(repo, worktree, runDirectory, runArea, netDir);
    }

    private SandboxRequest Build(string repo, string worktree, string runDirectory, string runArea, string netDir) =>
        new(
            LaunchId.From("LCH-" + new string('f', 32)),
            ItemId.From("ITM-" + new string('f', 32)),
            AgentId.From("claude-code"),
            RepoName.From("fixture"),
            Instance: null,
            new SandboxPaths(repo, Path.Combine(repo, ".git"), worktree, runDirectory, runArea),
            "sindri/fixture/1",
            new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null),
            Attended: false,
            StepTimeout: TimeSpan.FromMinutes(5),
            LongMax: null,
            SlotHeld: false,
            new SandboxNetFacts(netDir, SindriPort: 7480, ProxyPort: 41234),
            BoxHostFacts.Read(environment, sindriHome),
            Settings: null,
            Login: null);

    /// <summary>Gets the short directory for unix sockets: $XDG_RUNTIME_DIR/sindri, else /tmp/sindri-(uid) (a socket path is limited to 104 bytes).</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The directory.</returns>
    internal static string NetParent(Func<string, string?> environment) =>
        environment("XDG_RUNTIME_DIR") is { Length: > 0 } runtime
            ? Path.Combine(runtime, "sindri")
            : "/tmp/sindri-" + Sessions.OwnUid.Read().ToString(System.Globalization.CultureInfo.InvariantCulture);
}
