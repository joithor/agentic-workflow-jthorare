using Aw.Kernel;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Runs a command in the reproduction sandbox of an ended launch ('sindri sandbox exec', sandbox spec §3.6); the CLI verb's port, so its tests need no bubblewrap.</summary>
internal interface ISandboxExec
{
    /// <summary>Runs <paramref name="command"/> with this process's terminal and waits for it.</summary>
    /// <param name="stored">The launch's stored plan.</param>
    /// <param name="command">The command and its arguments.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The command's exit code, or the reason no sandbox could be made.</returns>
    public Task<Result<int>> Run(SandboxPlan stored, IReadOnlyList<string> command, CancellationToken cancellationToken);
}
