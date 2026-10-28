using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The sandbox step's launch-specific parts (<see cref="ISandboxLaunchStages"/>): the plan request from the approved profile, the login and the learn history, the egress, and what
/// the end of a launch releases. The session home itself is made and removed by <see cref="SandboxLaunch"/>.
/// </summary>
#pragma warning disable MA0182 // registered by the product Host in SB-5a's switch step
internal sealed class SandboxLaunchStages(IApprovedProfile profile, ILearnHistory learn, IReadOnlyList<IModelCredentialSource> logins, ISandboxLaunchInputs inputs, ILaunchEgress egress, TimeProvider clock) : ISandboxLaunchStages
{
    /// <inheritdoc/>
    public async ValueTask<Result<SandboxRequest>> Request(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var loaded = approved.Value?.Profile;
        var settings = loaded?.Sandbox;
        var repo = RepoNameOf(loaded, request.RepoPath);
        var prepared = await Login(request, settings, repo, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var history = await learn.Read(repo.Value, cancellationToken).ConfigureAwait(false);
        var common = await inputs.GitCommonDir(request.RepoPath, cancellationToken).ConfigureAwait(false);
        var run = StepEnvironment.For(request).GetValueOrDefault(StepEnvironment.RunDirectory, request.Worktree ?? request.RepoPath);
        return new SandboxRequest(
            request.Launch, request.Item, request.Agent, repo, request.Instance,
            new SandboxPaths(request.RepoPath, common, request.Worktree, run, inputs.RunAreaFor(request.Launch)),
            request.Instance is { } instance && request.Run is { } token ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"sindri/{instance.Value}/{token.Number}") : string.Empty,
            request.Step?.Policy, request.Attended,
            StepTimeout: null, LongMax: null, SlotHeld: false,   // not on the launch request: TODO(lead) the Step's timeout, longMax and heavy-slot fact
            new SandboxNetFacts(inputs.NetDirFor(request.Launch), inputs.SindriPort(), inputs.ProxyPort()),
            inputs.Host(), settings, prepared.Value)
        {
            Learn = new LearnContext(history, clock.GetUtcNow()),
        };
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WritePolicy(SandboxPlan plan, string sessionHome, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(Unit.Value);   // TODO(policy builder): StepPolicyFile.Write(..., SandboxPolicyOverlay.From(plan)) once it is on this branch

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return egress.Start(plan.Launch, plan.Network, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) =>
        egress.Release(launch);   // TODO(policy builder): DenialCollector.Collect(...) before this, and before session.ended

    private static RepoName RepoNameOf(SindriProfile? loaded, string repoPath) =>
        RepoName.From(loaded?.Repos?.FirstOrDefault(r => string.Equals(r.Path.TrimEnd('/'), repoPath.TrimEnd('/'), StringComparison.Ordinal))?.Name ?? Path.GetFileName(repoPath.TrimEnd('/')));

    private async ValueTask<Result<PreparedLogin>> Login(LaunchRequest request, SandboxSettings? settings, RepoName repo, CancellationToken cancellationToken)
    {
        var source = LoginSourceResolver.Resolve(settings, request.Agent, repo, request.Step?.Policy?.LoginSource);
        var chosen = logins.FirstOrDefault(l => l.Id == source && l.Supports(request.Agent));
        return chosen is null
            ? SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin, $"no {source} login source supports {request.Agent.Value}", source)
            : await chosen.Prepare(request.Agent, request.Launch, cancellationToken).ConfigureAwait(false);
    }
}
#pragma warning restore MA0182
