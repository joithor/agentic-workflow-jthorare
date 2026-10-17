using Aw.Kernel;
using Aw.Sindri.Features.ApproveProfile;

namespace Aw.Sindri.Host.Onyx;

/// <summary>Approves the live profile at the terminal, as one step of `onyx up`.</summary>
internal interface IProfileApprover
{
    /// <summary>Shows the live profile's hash and what changed, asks the human to type the challenge's text and approves; a profile that is already approved is returned without a prompt.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The control plane's answer; SND-PROFILE-004 when the human didn't type the text (a decline); another error when the approval itself failed.</returns>
    public Task<Result<ApproveProfileResponse>> Approve(CancellationToken cancellationToken);
}
