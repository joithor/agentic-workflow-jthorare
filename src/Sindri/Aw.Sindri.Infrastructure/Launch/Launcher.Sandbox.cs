using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The sandbox step of a Step launch (sandbox spec §12 SB-5a): the argv and environment it runs with, and the release of what it prepared.</summary>
internal sealed partial class Launcher
{
    /// <summary>What a launch runs with after the sandbox step: the argv, the environment and the summary (null when it ran unconfined).</summary>
    private sealed record Confined(IReadOnlyList<string> Argv, Dictionary<string, string> Env, SandboxSummary? Summary);

    /// <summary>An unattended launch with a sandbox runs its wrapped argv and sees the session home as AW_SINDRI_HOME, never the real one; anything else runs as built.</summary>
    private async ValueTask<Result<Confined>> Confine(LaunchRequest request, IReadOnlyList<string> argv, Dictionary<string, string> env, CancellationToken cancellationToken)
    {
        if (request.Attended || parts.Sandbox is not { } sandbox)
        {
            return new Confined(argv, env, Summary: null);
        }

        var prepared = await sandbox.Prepare(request, argv, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        env["AW_SINDRI_HOME"] = prepared.Value.SessionHome;
        return new Confined(prepared.Value.Argv, env, prepared.Value.Summary);
    }

    /// <summary>Releases the launch's sandbox (egress, session home, denials) once its session is over or its start failed; a no-op for a launch with none, and a failure never fails the stop.</summary>
    private async ValueTask ReleaseSandbox(LaunchId launch)
    {
        if (parts.Sandbox is not { } sandbox)
        {
            return;
        }

        try
        {
            await sandbox.Release(launch, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // what the release left behind is the crash sweep's; the stop itself went through
        }
    }
}
