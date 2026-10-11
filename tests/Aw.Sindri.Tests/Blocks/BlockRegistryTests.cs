using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Builtin;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>The block registry: ids unique, kinds and interfaces agree, typed ports parse their values.</summary>
public sealed class BlockRegistryTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_builtin_blocks_register_with_their_declarations()
    {
        var registry = Registries.Builtin();

        Assert.Equal(13, registry.List().Count);
        Assert.NotNull(registry.Gate(BlockId.From("gate.checks.passed")));
        Assert.Null(registry.Gate(BlockId.From("step.checks")));
        Assert.Equal(EffectAllowlist.NotifyHuman, registry.Effect(BlockId.From("effect.notify.human"))?.Spec.CommandKind);
        Assert.True(registry.Find(BlockId.From("step.produce"))?.Capability);
        Assert.Equal("checks.verdict:verdict", registry.Find(BlockId.From("step.checks"))?.Outputs.Single().ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_broken_registration_throws_at_construction()
    {
        var gateWithoutRung = new DeclaredBlock(new BlockSpec(BlockId.From("gate.test.bad"), BlockKind.Gate, "x", [], []));

        _ = Assert.Throws<InvalidOperationException>(() => Registries.Builtin(BuiltinSteps.Triage));
        _ = Assert.Throws<InvalidOperationException>(() => Registries.Builtin(gateWithoutRung));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(PortType.Verdict, "pass", true)]
    [InlineData(PortType.Verdict, "ok", false)]
    [InlineData(PortType.Size, "XL", true)]
    [InlineData(PortType.Size, "xl", false)]
    [InlineData(PortType.Number, "-1.5", true)]
    [InlineData(PortType.Flag, "yes", false)]
    [InlineData(PortType.Sha, "abc1234", true)]
    [InlineData(PortType.Text, " ", false)]
    public void A_port_accepts_only_values_of_its_type(PortType type, string value, bool accepted) =>
        Assert.Equal(accepted, new PortSpec("x", type).Accepts(value));
}
