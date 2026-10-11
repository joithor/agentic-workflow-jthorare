using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.RegisterEvidence;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.RegisterEvidence;

public sealed class RegisterEvidenceTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task RegisterEvidence_records_the_kind_for_the_sha()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var sha = HeadSha.From(new string('a', 40));

        var registered = (await new RegisterEvidenceService(h.Engine).Handle(new RegisterEvidenceRequest(i.Id, "test", sha, "sha256:00"), Token)).Value.Instance;

        var evidence = Assert.Single(registered.Evidence);
        Assert.Equal(("test", sha), (evidence.Kind, evidence.Sha));
    }
}
