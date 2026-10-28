using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.AllowDenial;

/// <summary>
/// The Allow button (sandbox spec §3.6): checks that the run really was blocked on the target and that the fix is "allow", writes the entry into the live profile under the run's repo, and says whether the approved
/// profile already holds it or the owner still has to approve.
/// </summary>
/// <param name="files">The runs' counters.</param>
/// <param name="owners">The repo of each launch.</param>
/// <param name="writer">Edits the live profile.</param>
/// <param name="approved">The approved profile.</param>
internal sealed class AllowDenialService(IDenialFiles files, ILaunchOwners owners, IProfileSandboxWriter writer, IApprovedProfile approved) : IService<AllowDenialRequest, AllowDenialResponse>
{
    public ValueTask<Result<AllowDenialResponse>> Handle(AllowDenialRequest request, CancellationToken cancellationToken)
    {
        _ = (files, owners, writer, approved);
        throw new NotSupportedException();
    }
}
