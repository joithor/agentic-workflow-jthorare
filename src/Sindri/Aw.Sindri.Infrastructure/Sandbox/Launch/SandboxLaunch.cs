using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The launcher's sandbox step (sandbox spec §12 SB-5a) for one unattended launch: probe, then, under the session cap, request, plan, session home, egress, self-check and wrap,
/// inside a systemd scope. A failure after the plan discards what the wrap left and releases the launch's parts; <see cref="Release"/> does the same at the end of a session.
/// </summary>
internal sealed class SandboxLaunch(ISandbox sandbox, ISessionCap cap, ISandboxLaunchStages stages, SandboxLaunchOptions options) : ISandboxLaunch
{
    private readonly ConcurrentDictionary<LaunchId, SandboxPlan> _plans = new();

    /// <inheritdoc/>
    public async ValueTask<Result<PreparedSandbox>> Prepare(LaunchRequest request, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(argv);
        var probed = sandbox.Probe();
        if (probed.IsError)
        {
            return probed.Error;
        }

        var asked = await stages.Request(request, cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        var limit = asked.Value.Settings?.Limits?.Sessions ?? SandboxDefaults.Sessions;
        var prepared = await cap.Start(limit, ct => Confine(asked.Value, argv, ct), cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            await Undo(request.Launch, CancellationToken.None).ConfigureAwait(false);
        }

        return prepared;
    }

    /// <inheritdoc/>
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => Undo(launch, cancellationToken);

    private async Task<Result<PreparedSandbox>> Confine(SandboxRequest request, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        var planned = sandbox.Plan(request);
        if (planned.IsError)
        {
            return planned.Error;
        }

        var plan = planned.Value;
        _plans[plan.Launch] = plan;
        var home = await Stage(plan, cancellationToken).ConfigureAwait(false);
        if (home.IsError)
        {
            return home.Error;
        }

        var checkedPlan = await sandbox.SelfCheck(plan, cancellationToken).ConfigureAwait(false);
        if (checkedPlan.IsError)
        {
            return checkedPlan.Error;
        }

        var wrapped = sandbox.Wrap(plan, argv);
        if (wrapped.IsError)
        {
            return wrapped.Error;
        }

        var limits = ScopeLimits.From(request.Settings?.Limits, options.Box, request.StepTimeout, options.LongMax, request.Attended);
        var scoped = SystemdScopeArgv.Start(options.SystemdRun, plan.Launch, limits, wrapped.Value);
        return new PreparedSandbox(plan.ToSummary(), scoped, home.Value, plan.RunDirectory);
    }

    /// <summary>The host side before the self-check: the session home, the policy files in it, and the egress.</summary>
    private async ValueTask<Result<string>> Stage(SandboxPlan plan, CancellationToken cancellationToken)
    {
        var home = MakeHome(plan);
        if (home.IsError)
        {
            return home.Error;
        }

        var policy = await stages.WritePolicy(plan, home.Value, cancellationToken).ConfigureAwait(false);
        if (policy.IsError)
        {
            return policy.Error;
        }

        var egress = await stages.StartEgress(plan, cancellationToken).ConfigureAwait(false);
        return egress.IsError ? egress.Error : home;
    }

    /// <summary>Makes the session home with the launch's one-read secret moved in (the launcher's own file is deleted: the wrap reads the home's) and control.json as a file, which a sandbox needs.</summary>
    private Result<string> MakeHome(SandboxPlan plan)
    {
        if (options.Control() is not { } control)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the control plane's endpoint is not known");
        }

        var file = LaunchSecrets.SecretPathFor(options.LaunchSecretsDirectory, plan.Launch);
        try
        {
            var home = SessionHome.Create(options.SindriHome, plan.Launch, File.ReadAllText(file), control);
            File.Delete(file);
            return home;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the session home could not be made: " + e.Message);
        }
    }

    private async ValueTask Undo(LaunchId launch, CancellationToken cancellationToken)
    {
        if (_plans.TryRemove(launch, out var plan))
        {
            sandbox.Discard(plan);
        }

        SessionHome.Remove(SessionHomePaths.For(options.SindriHome, launch));

        await stages.Release(launch, cancellationToken).ConfigureAwait(false);
    }
}
