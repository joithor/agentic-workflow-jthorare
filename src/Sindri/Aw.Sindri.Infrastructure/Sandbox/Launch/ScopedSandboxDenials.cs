using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox.Denials;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>ISandboxDenials for the singleton launcher side: <see cref="LaunchSessionEvents"/> is scoped (it holds the lease), so each append runs in its own scope, as the launcher's EndLive does.</summary>
/// <param name="scopes">Makes the scope.</param>
internal sealed class ScopedSandboxDenials(IServiceScopeFactory scopes) : ISandboxDenials
{
    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Append(SandboxDenied denied, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredService<LaunchSessionEvents>().Append(denied, cancellationToken).ConfigureAwait(false);
        }
    }
}
