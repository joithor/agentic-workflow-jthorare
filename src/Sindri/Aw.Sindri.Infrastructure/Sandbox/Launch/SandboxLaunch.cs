using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
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
            await Undo(request.Launch).ConfigureAwait(false);
        }

        return prepared;
    }

    /// <inheritdoc/>
    public async ValueTask Release(LaunchId launch, CancellationToken cancellationToken)
    {
        await Undo(launch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<PreparedSandbox>> Confine(SandboxRequest request, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        var planned = sandbox.Plan(request);
        if (planned.IsError)
        {
            return planned.Error;
        }

        var plan = planned.Value;
        _plans[plan.Launch] = plan;
        var home = await stages.MakeHome(plan, cancellationToken).ConfigureAwait(false);
        if (home.IsError)
        {
            return home.Error;
        }

        var egress = await stages.StartEgress(plan, cancellationToken).ConfigureAwait(false);
        if (egress.IsError)
        {
            return egress.Error;
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

    private async ValueTask Undo(LaunchId launch, CancellationToken cancellationToken = default)
    {
        if (_plans.TryRemove(launch, out var plan))
        {
            sandbox.Discard(plan);
        }

        await stages.Release(launch, cancellationToken).ConfigureAwait(false);
    }
}
