using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>Stands in for the stages until the flow part lands its implementation: registered so the container resolves, throws if a launch reaches it (no launch does while <c lang="text">LauncherParts.Sandbox</c> is unset).</summary>
internal sealed class PendingSandboxLaunchStages : ISandboxLaunchStages
{
    public ValueTask<Result<SandboxRequest>> Request(LaunchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> WritePolicy(SandboxPlan plan, string sessionHome, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();
}
