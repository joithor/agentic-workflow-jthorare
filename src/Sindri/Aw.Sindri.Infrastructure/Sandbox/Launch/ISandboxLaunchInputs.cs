using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The box- and Host-specific facts <see cref="SandboxLaunchStages"/> cannot get from the launch or the profile (the switch step fills them from the real box).</summary>
internal interface ISandboxLaunchInputs
{
    /// <summary>The host facts the plan builder reads: home, state dir, real Sindri home, installed binaries, uid, RAM, cores, environment allowlist source, other homes, hook inventory.</summary>
    /// <returns>The facts.</returns>
    public SandboxHostFacts Host();

    /// <summary>The repo's common git directory (<c lang="text">git rev-parse --git-common-dir</c>, absolute).</summary>
    /// <param name="repoPath">The repo checkout.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The path.</returns>
    public ValueTask<string> GitCommonDir(string repoPath, CancellationToken cancellationToken);

    /// <summary>The launcher's per-run area for the launch (scratch, shims, hook spool, plan file, wrap limits).</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The path.</returns>
    public string RunAreaFor(LaunchId launch);

    /// <summary>The short per-launch socket directory on the host.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The path.</returns>
    public string NetDirFor(LaunchId launch);

    /// <summary>The control plane's port (what the inside forwarder listens on and the host forwarder dials).</summary>
    /// <returns>The port.</returns>
    public int SindriPort();

    /// <summary>The port the plan tells the agent's proxy variables to use inside the sandbox.</summary>
    /// <returns>The port.</returns>
    public int ProxyPort();
}
