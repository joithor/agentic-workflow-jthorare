using Aw.Kernel;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Host.Onyx;

namespace Aw.Sindri.Host.Commands;

/// <summary>IProfileApprover over `profile approve`: the same preview, the same typed challenge, through the same verb (which starts the control plane when it must).</summary>
/// <param name="verbs">The verbs over the control plane.</param>
/// <param name="output">Where the preview goes (standard error under `onyx up`, so standard output stays the summary or the JSON).</param>
/// <param name="input">Where the typed answer is read.</param>
internal sealed class ProfileApprover(Verbs verbs, Output output, TextReader input) : IProfileApprover
{
    /// <inheritdoc />
    public async Task<Result<ApproveProfileResponse>> Approve(CancellationToken cancellationToken)
    {
        var shown = await verbs.Decide(hashText: null, interactive: true, input, cancellationToken).ConfigureAwait(false);
        if (shown.IsError || shown.Value.Approved)
        {
            return shown;   // nothing to approve, or the control plane couldn't say what there is
        }

        await output.Out.WriteAsync(Verbs.ApprovalText(shown.Value, preview: true)).ConfigureAwait(false);
        return await verbs.Decide(shown.Value.Hash.Value, interactive: true, input, cancellationToken).ConfigureAwait(false);
    }
}
