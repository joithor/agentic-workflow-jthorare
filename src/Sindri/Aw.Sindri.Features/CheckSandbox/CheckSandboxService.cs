using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>Runs the sandbox checks and words their blockers (sandbox spec §6, §8). Read-only: it resumes nothing.</summary>
/// <param name="checks">The checks.</param>
internal sealed class CheckSandboxService(ISandboxChecks checks) : IService<CheckSandboxRequest, CheckSandboxResponse>
{
    public async ValueTask<Result<CheckSandboxResponse>> Handle(CheckSandboxRequest request, CancellationToken cancellationToken)
    {
        var report = await checks.Run(cancellationToken).ConfigureAwait(false);
        return new CheckSandboxResponse([.. report.Blockers.Select(SandboxBlockerLine.From)], report.ReadyToResume, report.Ready);
    }
}
