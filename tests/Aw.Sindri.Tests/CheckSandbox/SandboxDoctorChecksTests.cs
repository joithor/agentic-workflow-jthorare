using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>Doctor's sandbox rows say what the Check again card says (sandbox spec §8): one sentence per (code, reason), the command as the fix.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxDoctorChecksTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SandboxBlocker Blocker(ErrorDefinitionPair pair, ModelLoginSource? source = null) =>
        SandboxBlocker.Of(SandboxRefusal.For(pair.Error, pair.Reason, source: source));

    private static Task<IReadOnlyList<DoctorCheck>> Run(ISandboxChecks checks, ISandbox? sandbox = null) =>
        new SandboxDoctorChecks(checks, sandbox ?? new PassThroughSandbox(), TimeProvider.System).Run(Token).AsTask();

    [Fact]
    public async Task A_ready_box_says_so_with_the_mechanism_and_the_checks_passed()
    {
        var rows = await Run(new Report());

        var row = Assert.Single(rows);
        Assert.Equal(("sandbox", CheckState.Ok), (row.Name, row.State));
        Assert.StartsWith("sandbox ready: test-only pass-through, 4 checks passed in ", row.Detail, StringComparison.Ordinal);
        Assert.EndsWith(" ms", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_blocker_is_its_golden_sentence_with_the_command_as_the_fix()
    {
        var rows = await Run(new Report(Blocker(new(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoUserManager))));

        var row = Assert.Single(rows);
        Assert.Equal(("sandbox", CheckState.Fail), (row.Name, row.State));
        Assert.Equal("The sandbox needs your user session manager, which isn't running. Run `loginctl enable-linger $USER`, then press Check again.", row.Detail);
        Assert.Equal("loginctl enable-linger $USER", row.Fix);
    }

    [Fact]
    public async Task A_login_blocker_is_the_login_row_and_picks_its_sentence_by_source()
    {
        var rows = await Run(new Report(Blocker(new(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin), ModelLoginSource.OwnerCopy)));

        var row = Assert.Single(rows);
        Assert.Equal(("sandbox:login", CheckState.Fail), (row.Name, row.State));
        Assert.Equal("Claude Code (or Codex) isn't signed in for Steps. Sign in as yourself, then press Check again.", row.Detail);
    }

    [Fact]
    public async Task Unsupported_platform_is_gray_information_never_a_fault()
    {
        var rows = await Run(new Report(Blocker(new(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.UnsupportedPlatform))));

        var row = Assert.Single(rows);
        Assert.Equal(CheckState.Ok, row.State);
        Assert.Null(row.Fix);
        Assert.Equal("Steps can't run by themselves on macOS yet.", row.Detail);
    }

    [Fact]
    public async Task Every_blocker_gets_its_own_row()
    {
        var rows = await Run(new Report(
            Blocker(new(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoForwarder)),
            Blocker(new(SindriErrors.LaunchUnconfinable, SandboxReasons.FormNone))));

        Assert.Equal(2, rows.Count);
    }

    internal readonly record struct ErrorDefinitionPair(Aw.Kernel.ErrorDefinition Error, string Reason);

    private sealed class Report(params SandboxBlocker[] blockers) : ISandboxChecks
    {
        public ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken) => ValueTask.FromResult(new SandboxCheckReport(blockers, Array.Empty<WorkflowInstanceId>()));
    }
}
