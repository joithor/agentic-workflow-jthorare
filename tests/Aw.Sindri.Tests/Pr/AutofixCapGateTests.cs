using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Pr;

/// <summary>'gate.pr.autofix-cap' (PR babysitter spec §4.8, IDEA-016): CI fixes pushed to one pull request, counted across instances in the ledger, stop at the profile's cap with the failing check's log.</summary>
[Lane(TestLane.Unit)]
public sealed class AutofixCapGateTests
{
    private const string Other = "https://forge.test/acme/app/pull/8";

    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly FakePullRequests _pulls = new();
    private readonly PushedLedger _ledger;

    public AutofixCapGateTests()
    {
        _ledger = new PushedLedger(_facts);
        _pulls.Add(PrContexts.Pull(failing: ["build"]));
        _pulls.SetLog(PrContexts.Link, "build", "build failed: assertion at Foo.cs:12");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<GateResult> Evaluate(int? cap = null)
    {
        _ = await _profiles.Save(new LoadedProfile(new SindriProfile(1, "dev", Workflow: new WorkflowSettings(Pr: cap is null ? null : new PrSettings(cap))), ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), Token);
        return await new AutofixCapGate(_ledger, _facts, _profiles, _pulls).Evaluate(
            PrContexts.Gate("gate.pr.autofix-cap", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = $"\"{PrContexts.Link}\"" }), Token);
    }

    [Fact]
    public async Task A_pull_request_with_no_fix_yet_passes() => Assert.Equal(GateVerdict.Pass, (await Evaluate()).Verdict);

    [Fact]
    public async Task Fixes_below_the_default_cap_pass()
    {
        _ledger.Push(PrContexts.Link, "ci");
        _ledger.Push(PrContexts.Link, "ci", "pushedSource");

        Assert.Equal(GateVerdict.Pass, (await Evaluate()).Verdict);
    }

    [Fact]
    public async Task Fixes_from_two_instances_count_together_and_stop_at_the_default_cap_of_three()
    {
        _ledger.Push(PrContexts.Link, "ci");   // instance one
        _ledger.Push(PrContexts.Link, "ci", "pushedSource");   // instance one
        _ledger.Push(PrContexts.Link, "ci");   // instance two

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("3 CI fixes", StringComparison.Ordinal) && r.Contains("--grant", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_refusal_shows_the_failing_checks_log()
    {
        _ledger.Push(PrContexts.Link, "ci");

        var result = await Evaluate(cap: 1);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("assertion at Foo.cs:12", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_refusal_still_refuses_when_the_log_cannot_be_read()
    {
        _ledger.Push(PrContexts.Link, "ci");
        _pulls.Change(PrContexts.Link, static p => p with { Checks = new PullChecks(PullChecksState.Failing, p.HeadSha, ["unlogged"]) });

        var result = await Evaluate(cap: 1);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("1 CI fix", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_profile_cap_moves_the_line()
    {
        _ledger.Push(PrContexts.Link, "ci");
        _ledger.Push(PrContexts.Link, "ci");
        _ledger.Push(PrContexts.Link, "ci");

        Assert.Equal(GateVerdict.Pass, (await Evaluate(cap: 4)).Verdict);
        Assert.Equal(GateVerdict.Fail, (await Evaluate(cap: 3)).Verdict);
    }

    [Fact]
    public async Task Another_pull_request_another_cause_and_pushes_that_did_nothing_do_not_count()
    {
        _ledger.Push(Other, "ci");
        _ledger.Push(PrContexts.Link, "review");
        _ledger.Push(PrContexts.Link, "ci", "headMoved");
        _ledger.Push(PrContexts.Link, "ci", "nothing");
        _ledger.Push(PrContexts.Link, "ci", "rejected");
        _ledger.Push(PrContexts.Link, "ci", type: "pr.changed");

        Assert.Equal(GateVerdict.Pass, (await Evaluate(cap: 1)).Verdict);
    }

    [Fact]
    public async Task With_no_approved_profile_the_default_cap_applies()
    {
        var result = await new AutofixCapGate(_ledger, _facts, new InMemoryProfiles(), _pulls).Evaluate(
            PrContexts.Gate("gate.pr.autofix-cap", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = $"\"{PrContexts.Link}\"" }), Token);

        Assert.Equal(GateVerdict.Pass, result.Verdict);   // no approved profile: the default cap of three, nothing pushed yet
    }
}
