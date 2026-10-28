using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.GetSandboxStatus;

/// <summary>Says what the last sandbox check found and how the sandbox is set up (sandbox spec §8). Read-only and cheap: it runs no check and no probe.</summary>
/// <param name="last">The newest check.</param>
/// <param name="approved">The approved profile's sandbox section.</param>
/// <param name="machine">The memory and cores the limits are read against.</param>
/// <param name="learn">The ledger's learn history.</param>
/// <param name="clock">Reads the time the learn windows are measured at.</param>
internal sealed class GetSandboxStatusService(LastSandboxCheck last, IApprovedProfile approved, ISandboxMachine machine, ILearnHistory learn, TimeProvider clock)
    : IService<GetSandboxStatusRequest, SandboxStatusResponse>
{
    public ValueTask<Result<SandboxStatusResponse>> Handle(GetSandboxStatusRequest request, CancellationToken cancellationToken)
    {
        _ = (last, approved, machine, learn, clock);
        throw new NotSupportedException("the status is not read yet");
    }
}
