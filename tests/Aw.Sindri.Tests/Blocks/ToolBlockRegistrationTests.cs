using Aw.Kernel.Testing;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>The test registry (Registries.Builtin) lists the same blocks AddSindriCore registers in production.</summary>
[Lane(TestLane.Unit)]
public sealed class ToolBlockRegistrationTests
{
    [Fact]
    public async Task The_test_registry_matches_what_AddSindriCore_registers()
    {
        await using var h = new WorkflowHarness();

        var registered = h.Blocks.List().Select(s => s.Id.Value).Where(id => id != "gate.test.model");

        Assert.Equal(Registries.Builtin().List().Select(s => s.Id.Value), registered, StringComparer.Ordinal);
    }
}
