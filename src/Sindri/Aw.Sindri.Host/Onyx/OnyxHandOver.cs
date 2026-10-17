using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Features.RunDoctor;

namespace Aw.Sindri.Host.Onyx;

/// <summary>
/// Step 10 of `onyx up` (spec §4.1): once the profile is written, approve it inline, read doctor's `context:onyx` row, open Onyx's connectors page and say what is left to do.
/// It runs in the Host because approving needs the control plane, which a service can't reach. A human who doesn't type the challenge leaves the profile written and unapproved.
/// </summary>
/// <param name="approver">Approves the profile at the terminal.</param>
/// <param name="services">The scope the doctor service is resolved from.</param>
/// <param name="browser">Opens the connectors page.</param>
internal sealed class OnyxHandOver(IProfileApprover approver, IServiceProvider services, IBrowser browser)
{
    private const string OnyxRow = "context:onyx";

    /// <summary>Approves, checks, opens and words the result.</summary>
    /// <param name="up">What the steps did (the last one is `profile`, none needs the human).</param>
    /// <param name="cancellationToken">Cancels the hand-over.</param>
    /// <returns>The response with the profile, doctor and connectors filled in, the text for the terminal, and the approval's error when it failed for a reason other than the human declining.</returns>
    internal async Task<HandOverResult> Run(OnyxUpResponse up, CancellationToken cancellationToken)
    {
        var approval = await approver.Approve(cancellationToken).ConfigureAwait(false);
        if (approval.IsError && approval.Error.Code != SindriErrors.ProfileNeedsTerminal.Code)
        {
            return new HandOverResult(up, string.Empty, approval.Error);
        }

        var profile = approval.IsSuccess && approval.Value.Approved ? "approved" : "written-unapproved";
        var doctor = await Row(cancellationToken).ConfigureAwait(false);
        var connectors = OnyxPages.Connectors(up.Port);
        var opened = browser.Open(connectors);
        return new HandOverResult(up with { Profile = profile, Doctor = doctor, Connectors = connectors.AbsoluteUri }, Text(connectors, opened), Failure: null);
    }

    // Only the context:onyx row is read, from doctor run in this process; no row (doctor itself failed) is simply not shown.
    private async Task<DoctorCheck?> Row(CancellationToken cancellationToken)
    {
        var report = await services.GetRequiredService<IService<RunDoctorRequest, RunDoctorResponse>>().Handle(new RunDoctorRequest(), cancellationToken).ConfigureAwait(false);
        return report.IsError ? null : report.Value.Checks.FirstOrDefault(static c => string.Equals(c.Name, OnyxRow, StringComparison.Ordinal));
    }

    private static string Text(Uri connectors, bool opened)
        => (opened ? $"Opened Onyx's connectors page: {connectors}" : $"Open Onyx's connectors page yourself: {connectors}")
            + "\nNext, connect your sources there (company OAuth: get sign-off first):"
            + "\n  Linear: your product's teams, and a start date that covers the backtests"
            + "\n  Slack: your product's channels\n";
}
