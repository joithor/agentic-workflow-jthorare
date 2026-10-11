using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Blocks;

[Lane(TestLane.Unit)]
public sealed class FakeUiEvidenceContractTests : UiEvidenceContract
{
    protected override IUiEvidence Create(UiEvidenceScenario scenario)
    {
        var fake = new FakeUiEvidence { RunsPass = scenario.RunPasses };
        fake.Health = scenario.Health ?? fake.Health;
        fake.DatabaseProvenance = scenario.Provenance ?? fake.DatabaseProvenance;
        fake.Failure = scenario.Error is { } line ? SindriErrors.ToolUnavailable.With($"ui-evidence exited 4: ui-evidence: {line}") : null;
        return fake;
    }

    protected override string RunDirectory() => "/runs/r1";
}
