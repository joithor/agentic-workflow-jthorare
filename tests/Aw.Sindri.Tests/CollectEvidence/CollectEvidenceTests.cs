using Aw.Sindri.Core.Ids;
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

        var collected = (await new CollectEvidenceService().Handle(Request(i.Id), Token)).Value;

        Assert.True(collected.Passed);
        var evidence = Assert.Single(collected.Registered!.Instance.Evidence);   // why: a passing run is registered
        Assert.Equal(("ui-browser", _sha, collected.Digest), (evidence.Kind, evidence.Sha, evidence.Digest));
    }

    private static CollectEvidenceRequest Request(InstanceId id) =>
        new(id, "ui-browser", _sha, new Dictionary<string, string>(StringComparer.Ordinal) { ["script"] = "/s.json", ["run-dir"] = "/runs/r1", ["host"] = "http://localhost:3000" });
}
