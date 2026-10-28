using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// Check again (sandbox spec §6, §8): the box probe, the form check, the login probe and a throw-away self-check on a fixture plan, every blocker on one report so the first
/// run is one visit, not four. When nothing blocks, it lists the parked instances it would resume: oldest first, no more than the free session slots.
/// </summary>
/// <param name="sandbox">The mechanism.</param>
/// <param name="forms">The form check.</param>
/// <param name="logins">The login probe.</param>
/// <param name="fixture">The fixture request.</param>
/// <param name="resume">The parked instances and the free slots.</param>
internal sealed class SandboxChecks(ISandbox sandbox, ISandboxFormCheck forms, ISandboxLoginCheck logins, ISandboxFixture fixture, SandboxResumeList resume) : ISandboxChecks
{
    public async ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken)
    {
        var blockers = new List<SandboxBlocker>();
        var probe = sandbox.Probe();
        if (probe.IsError)
        {
            blockers.Add(SandboxBlocker.Of(probe.Error));
        }

        blockers.AddRange(await forms.Check(cancellationToken).ConfigureAwait(false));
        blockers.AddRange(await logins.Check(cancellationToken).ConfigureAwait(false));
        if (probe.IsSuccess && await SelfCheck(cancellationToken).ConfigureAwait(false) is { } failed)
        {
            blockers.Add(failed);
        }

        return blockers.Count > 0
            ? new SandboxCheckReport(blockers, [])
            : new SandboxCheckReport(blockers, await resume.Next(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Plans the fixture and proves the plan inside the sandbox; the first failure is the blocker. No fixture on this box skips it.</summary>
    private async ValueTask<SandboxBlocker?> SelfCheck(CancellationToken cancellationToken)
    {
        if (fixture.Request() is not { } request)
        {
            return null;
        }

        var plan = sandbox.Plan(request);
        if (plan.IsError)
        {
            return SandboxBlocker.Of(plan.Error);
        }

        var proved = await sandbox.SelfCheck(plan.Value, cancellationToken).ConfigureAwait(false);
        return proved.IsError ? SandboxBlocker.Of(proved.Error) : null;
    }
}
