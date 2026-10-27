using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Asks the launcher for a session and waits out a full session cap (SND-LAUNCH-020, a queue condition and not a fault; sandbox spec §6): it asks again every
/// <see cref="AgentRuntimeOptions.SlotRetry"/> until <see cref="AgentRuntimeOptions.SlotSlice"/> ends, recording nothing.
/// </summary>
/// <param name="launcher">The launcher.</param>
/// <param name="options">How long to wait for a free session slot.</param>
internal sealed class AgentLaunchWaiting(ILauncher launcher, AgentRuntimeOptions options)
{
    /// <summary>Launches, asking again while the cap is full.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="cancellationToken">Cancels the launch and the wait.</param>
    /// <returns>The launcher's answer; null when the slice ended with the cap still reached.</returns>
    public async Task<LaunchResult?> Launch(LaunchRequest request, CancellationToken cancellationToken)
    {
        for (var waited = TimeSpan.Zero; ; waited += options.SlotRetry)
        {
            var result = await launcher.Launch(request, cancellationToken).ConfigureAwait(false);
            if (result is not FailedLaunch failed || SandboxCodes.KindOf(failed.Error.Code) != LaunchCodeKind.ResourceWait)
            {
                return result;
            }

            if (waited + options.SlotRetry > options.SlotSlice)
            {
                return null;
            }

            await Task.Delay(options.SlotRetry, cancellationToken).ConfigureAwait(false);
        }
    }
}
