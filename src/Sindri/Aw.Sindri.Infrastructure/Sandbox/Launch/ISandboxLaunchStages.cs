using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The parts of a launch's sandbox step that other SB-5a pieces write (the session home, the policy files, the egress registry, the denial collector): <see cref="SandboxLaunch"/>
/// calls them in this order and owns only the order, the cap, the scope wrap and the cleanup after a failure.
/// </summary>
internal interface ISandboxLaunchStages
{
    /// <summary>TODO(home+policy builders): builds the request from the launch: the repo's profile settings, host paths, net facts, learn history, and the prepared login (credentials).</summary>
    /// <param name="request">The launch.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The request, or the SND-LAUNCH-015/017 refusal.</returns>
    public ValueTask<Result<SandboxRequest>> Request(LaunchRequest request, CancellationToken cancellationToken);

    /// <summary>TODO(policy+egress builder): writes the policy files the plan names (claude settings, codex overrides) into the session home the adapter just made.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="sessionHome">The launch's session home.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or a refusal.</returns>
    public ValueTask<Result<Unit>> WritePolicy(SandboxPlan plan, string sessionHome, CancellationToken cancellationToken);

    /// <summary>TODO(policy+egress builder): starts the launch's egress (registered per launch) before the self-check.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or the SND-LAUNCH-016 refusal.</returns>
    public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken);

    /// <summary>TODO(policy+egress builder): what the end of a launch removes besides the session home (the adapter removes that): collects its denials and stops its egress. A launch with none is a no-op.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when it is gone.</returns>
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken);
}
