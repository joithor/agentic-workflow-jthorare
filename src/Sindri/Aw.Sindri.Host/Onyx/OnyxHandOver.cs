using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;

namespace Aw.Sindri.Host.Onyx;

/// <summary>
/// Step 10 of `onyx up` (spec §4.1): once the profile is written, approve it inline, run doctor's `context:onyx` row, open Onyx's connectors page and print what is left to do.
/// It runs in the Host because approving needs the control plane, which a service can't reach.
/// </summary>
/// <param name="approver">Approves the profile at the terminal.</param>
/// <param name="services">The scope the doctor service is resolved from.</param>
/// <param name="browser">Opens the connectors page.</param>
internal sealed class OnyxHandOver(IProfileApprover approver, IServiceProvider services, IBrowser browser)
{
    /// <summary>Approves, checks, opens and words the result.</summary>
    /// <param name="up">What the steps did (the last one is `profile`, none needs the human).</param>
    /// <param name="cancellationToken">Cancels the hand-over.</param>
    /// <returns>The response with the profile, doctor and connectors filled in, the text for the terminal, and an error when the approval itself failed.</returns>
    internal Task<HandOverResult> Run(OnyxUpResponse up, CancellationToken cancellationToken)
    {
        _ = (approver, services, browser, up, cancellationToken);
        throw new NotSupportedException();
    }
}
