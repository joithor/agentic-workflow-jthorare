using System.Globalization;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// Doctor's sandbox rows (sandbox spec §8): 'sandbox ready: …' when nothing blocks a Step, else one row per blocker with the same sentence the Check again card says and its
/// copyable command as the fix. A -017 blocker is the 'sandbox:login' row. A gray sentence (macOS: not yet supported) is information, never a fault.
/// </summary>
/// <param name="checks">The checks Check again runs.</param>
/// <param name="sandbox">The mechanism, for the ready line's version.</param>
/// <param name="clock">Times the checks.</param>
internal sealed class SandboxDoctorChecks(ISandboxChecks checks, ISandbox sandbox, TimeProvider clock) : IDoctorChecks
{
    /// <summary>The check groups a ready box passed: the probe, the form check, the login probe and the fixture self-check.</summary>
    internal const int Groups = 4;

    private const string LoginCode = "SND-LAUNCH-017";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var report = await checks.Run(cancellationToken).ConfigureAwait(false);
        if (report.Ready)
        {
            var probe = sandbox.Probe();
            var what = probe.IsSuccess ? probe.Value.Detail ?? "available" : "available";
            var ms = (long)clock.GetElapsedTime(started).TotalMilliseconds;
            return [new DoctorCheck("sandbox", CheckState.Ok, string.Create(CultureInfo.InvariantCulture, $"sandbox ready: {what}, {Groups} checks passed in {ms} ms"), Fix: null)];
        }

        return [.. report.Blockers.Select(static b => SandboxBlockerLine.From(b)).Select(Row)];
    }

    private static DoctorCheck Row(SandboxBlockerLine line) => new(
        string.Equals(line.Code, LoginCode, StringComparison.Ordinal) ? "sandbox:login" : "sandbox",
        line.Quiet ? CheckState.Ok : CheckState.Fail,
        line.Text,
        line.Quiet ? null : line.Command);
}
