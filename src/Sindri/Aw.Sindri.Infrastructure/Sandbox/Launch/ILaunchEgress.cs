using Aw.Kernel;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The launch's egress as the stages see it: started before the self-check, released at the end. TODO(switch step): an adapter over LaunchSandboxRegistry (policy+egress builder) plugs in here.</summary>
internal interface ILaunchEgress
{
    /// <summary>Starts the launch's forwarders for its plan's network.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="network">The plan's network.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>Success, or -014 (no forwarder) or -016 (net forward).</returns>
    public ValueTask<Result<Unit>> Start(LaunchId launch, PlanNetwork network, CancellationToken cancellationToken);

    /// <summary>Stops the launch's forwarders; idempotent, and a no-op for a launch with none.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>A task that completes when they are stopped.</returns>
    public ValueTask Release(LaunchId launch);
}
