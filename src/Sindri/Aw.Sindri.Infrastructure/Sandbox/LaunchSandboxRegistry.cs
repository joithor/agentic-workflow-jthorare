using Aw.Kernel;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The running egress of every launch in flight (sandbox spec §3.2, SB-5a): the launcher starts it before the self-check, the session's end disposes it
/// (which flushes <c lang="text">egress.log</c> and the denial counter, then removes the socket directory). Safe to call from any thread; disposing the
/// registry releases every launch still in it.
/// </summary>
/// <param name="service">Starts one launch's listeners.</param>
internal sealed class LaunchSandboxRegistry(EgressService service) : IAsyncDisposable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<LaunchId, RunningEgress> _running = [];
    private bool _disposed;

    /// <summary>Starts the launch's egress and keeps it. A launch started again replaces (and disposes) its old egress.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="network">The plan's network.</param>
    /// <param name="denials">The launch's denial counter, or null.</param>
    /// <param name="time">The clock the proxy checks the learn expiry against; null is the system clock.</param>
    /// <returns>The running egress, or the refusal of <see cref="EgressService.Start"/> (-014 no-forwarder, -016 net-forward); nothing is kept for a refusal.</returns>
    public Result<RunningEgress> Start(LaunchId launch, PlanNetwork network, DenialCounter? denials, TimeProvider? time = null)
    {
        var started = service.Start(network, EgressCaps.Default, denials, time);
        if (started.IsError)
        {
            return started;
        }

        RunningEgress? replaced;
        var refused = false;
        lock (_lock)
        {
            refused = _disposed;
            _ = _running.Remove(launch, out replaced);
            if (!refused)
            {
                _running[launch] = started.Value;
            }
        }

        if (refused)
        {
            // The registry is gone: nobody would release this one.
            started.Value.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return Aw.Sindri.Core.SindriErrors.NotRunning.With("the sandbox registry is disposed");
        }

        replaced?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return started;
    }

    /// <summary>The launch's running egress.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="egress">The egress when there is one.</param>
    /// <returns>True when the launch has one.</returns>
    public bool TryGet(LaunchId launch, out RunningEgress egress)
    {
        lock (_lock)
        {
            return _running.TryGetValue(launch, out egress!);
        }
    }

    /// <summary>Disposes and forgets the launch's egress.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>True when the launch had one; false for a launch with none or one already released.</returns>
    public async ValueTask<bool> Release(LaunchId launch)
    {
        RunningEgress? egress;
        lock (_lock)
        {
            _ = _running.Remove(launch, out egress);
        }

        if (egress is null)
        {
            return false;
        }

        await egress.DisposeAsync().ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        RunningEgress[] all;
        lock (_lock)
        {
            _disposed = true;
            all = [.. _running.Values];
            _running.Clear();
        }

        foreach (var egress in all)
        {
            await egress.DisposeAsync().ConfigureAwait(false);
        }
    }
}
