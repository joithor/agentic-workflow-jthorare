using Aw.Kernel;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Host.Onyx;

/// <summary>IProfileApprover over `profile approve`: the same preview, the same typed challenge, through the same verb.</summary>
/// <param name="verbs">The verbs over the control plane (started when needed).</param>
/// <param name="output">Where the preview goes (standard error under `onyx up`).</param>
/// <param name="input">Where the typed answer is read.</param>
internal sealed class ProfileApprover(Verbs verbs, Output output, TextReader input) : IProfileApprover
{
    /// <inheritdoc />
    public Task<Result<ApproveProfileResponse>> Approve(CancellationToken cancellationToken)
    {
        _ = (verbs, output, input, cancellationToken);
        throw new NotSupportedException();
    }
}
