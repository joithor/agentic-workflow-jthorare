using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Host.Onyx;

namespace Aw.Sindri.Tests.Commands;

/// <summary>An IProfileApprover that answers what a test scripts and counts its calls.</summary>
internal sealed class FakeProfileApprover : IProfileApprover
{
    /// <summary>Gets or sets what every call answers: approved by default.</summary>
    public Result<ApproveProfileResponse> Answer { get; set; } = new ApproveProfileResponse(ProfileHash.From(new string('a', 64)), [], Approved: true, Sequence: null);

    /// <summary>Gets the number of calls.</summary>
    public int Calls { get; private set; }

    public Task<Result<ApproveProfileResponse>> Approve(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Answer);
    }
}
