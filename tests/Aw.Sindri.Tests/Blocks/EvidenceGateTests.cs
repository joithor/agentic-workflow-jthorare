using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>ui-evidence's checks as rung-1 gates: doctor and provenance.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceGateTests
{
    private static readonly GateContext _context = new(InstanceId.From("wf-000000000001"), RecipeId.From("workflow.ui-evidence"), ItemId.From("ITEM-1"), 1, "stack", "run", "ready",
        new Dictionary<string, string>(StringComparer.Ordinal), [], Sha: null);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Doctor_passes_a_healthy_stack_and_refuses_with_the_failures()
    {
        var ui = new FakeUiEvidence();
        var gate = new StackDoctorGate(ui);

        var healthy = await gate.Evaluate(_context, Token);
        ui.Health = new StackHealth(Healthy: false, ["the database is not ready", "api-server (:5002) is not accepting connections"]);
        var sick = await gate.Evaluate(_context, Token);

        Assert.True(healthy.Value.Passed);
        Assert.False(sick.Value.Passed);
        Assert.Equal("ui-evidence doctor: the database is not ready; api-server (:5002) is not accepting connections", sick.Value.Detail);
        Assert.True(gate.Spec is { Kind: BlockKind.Gate, Rung: GateRung.Deterministic, Capability: false });
    }

    [Fact]
    public async Task Provenance_passes_seed_data_only()
    {
        var ui = new FakeUiEvidence();
        var gate = new ProvenanceGate(ui);

        var seeded = await gate.Evaluate(_context, Token);
        ui.DatabaseProvenance = new DatabaseProvenance(DataProvenance.Unknown, "no seed row found in public.profile");
        var unknown = await gate.Evaluate(_context, Token);

        Assert.True(seeded.Value.Passed);
        Assert.False(unknown.Value.Passed);
        Assert.Equal("ui-evidence provenance: unknown (no seed row found in public.profile)", unknown.Value.Detail);
    }

    [Fact]
    public async Task A_tool_that_cant_be_run_is_an_error_for_either_gate()
    {
        var ui = new FakeUiEvidence { Failure = SindriErrors.ToolUnavailable.With("ui-evidence can't be started") };

        Assert.Equal(SindriErrors.ToolUnavailable.Code, (await new StackDoctorGate(ui).Evaluate(_context, Token)).Error.Code);
        Assert.Equal(SindriErrors.ToolUnavailable.Code, (await new ProvenanceGate(ui).Evaluate(_context, Token)).Error.Code);
    }
}
