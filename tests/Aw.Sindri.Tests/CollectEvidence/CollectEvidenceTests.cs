using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.CollectEvidence;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.CollectEvidence;

public sealed class CollectEvidenceTests
{
    private static readonly HeadSha _sha = HeadSha.From(new string('e', 40));

    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task CollectEvidence_registers_a_passing_run_as_evidence()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var collected = (await Service(h).Handle(Request(i.Id), Token)).Value;

        Assert.True(collected.Passed);
        var evidence = Assert.Single(collected.Registered!.Instance.Evidence);   // why: a passing run is registered
        Assert.Equal(("ui-browser", _sha, collected.Digest), (evidence.Kind, evidence.Sha, evidence.Digest));
        Assert.Equal("/runs/r1/summary.json", collected.Document);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CollectEvidence_reports_a_failed_run_and_registers_nothing()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        h.UiEvidence.RunsPass = false;

        var collected = (await Service(h).Handle(Request(i.Id), Token)).Value;

        Assert.False(collected.Passed);
        Assert.Null(collected.Registered);
        Assert.Empty((await h.Get(i.Id)).Evidence);
        Assert.Equal(i.Version, (await h.Get(i.Id)).Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CollectEvidence_refuses_an_unknown_instance_or_kind_before_running_the_tool()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var unknownInstance = await Service(h).Handle(Request(InstanceId.From("wf-0000000000ff")), Token);
        var unknownKind = await Service(h).Handle(Request(i.Id) with { Kind = "test" }, Token);

        Assert.Equal(SindriErrors.InstanceUnknown.Code, unknownInstance.Error.Code);
        Assert.Equal(SindriErrors.ProviderUnknown.Code, unknownKind.Error.Code);
        Assert.Equal("no evidence provider for 'test' (providers: design-parity, ui-browser)", unknownKind.Error.Message);
        Assert.Empty(h.UiEvidence.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CollectEvidence_passes_a_tool_failure_through_and_registers_nothing()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        h.UiEvidence.Failure = SindriErrors.ToolUnavailable.With("ui-evidence run exited 75: ui-evidence: AW-EVIDENCE-009 lock busy");

        var collected = await Service(h).Handle(Request(i.Id), Token);

        Assert.Equal(SindriErrors.ToolUnavailable.Code, collected.Error.Code);
        Assert.Empty((await h.Get(i.Id)).Evidence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CollectEvidence_refuses_an_ended_instance_before_running_the_tool()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        _ = (await h.Engine.Apply(i.Id, new CancelCommand("dropped"), Token)).Value;

        var refused = await Service(h).Handle(Request(i.Id), Token);

        Assert.Equal(SindriErrors.TransitionNotAllowed.Code, refused.Error.Code);
        Assert.Empty(h.UiEvidence.Calls);
    }

    private static CollectEvidenceService Service(WorkflowHarness h) => new(h.Providers, h.Store, h.Engine);

    private static CollectEvidenceRequest Request(InstanceId id) =>
        new(id, "ui-browser", _sha, new Dictionary<string, string>(StringComparer.Ordinal) { ["script"] = "/s.json", ["run-dir"] = "/runs/r1", ["host"] = "http://localhost:3000" });
}
