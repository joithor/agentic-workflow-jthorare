using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Workflow.Ship;

/// <summary>'step.ship.freeze' on the block runtime (line spec §7.5, SHIP-SHA-001): the shas agree and the hand-off is machine-frozen, or the head moved.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipFreezeStepTests
{
    private const string Other = "1234567890123456789012345678901234567890";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Dictionary<string, string>> Packed(ShipRig rig, string? green = null)
    {
        var packed = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts(green: green)), Token);
        return ShipRig.FreezeFacts(packed.Result!.Outputs[FactKey.From("fact:handoffDraft")].GetRawText());
    }

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task A_hand_off_whose_shas_agree_is_machine_frozen_and_named_in_fact_handoff()
    {
        var rig = new ShipRig();
        var facts = await Packed(rig);

        var result = await rig.Freeze().Run(ShipRig.Context("step.ship.freeze", facts), Token);

        Assert.Equal("frozen", result.Result!.Outcome.Value);
        var (_, packet, by) = Assert.Single(rig.Packets.Calls, static c => c.Verb == "freeze");
        Assert.Equal((PacketKind.Handoff, PacketFreezer.Machine, "/checkouts/name"), (packet.Kind, by, packet.Repo));
        var handoff = Out(result, "handoff");
        Assert.Equal((InMemoryPackets.HashOf(packet), packet.Directory), ((string?)handoff["hash"], (string?)handoff["dir"]));
    }

    [Fact]
    public async Task A_review_of_another_head_is_stale_with_a_SHIP_SHA_001_finding_and_freezes_nothing()
    {
        var rig = new ShipRig();
        var facts = await Packed(rig);
        _ = rig.Files.With(ShipRig.ReviewDir, "review.json", $$"""{"schemaVersion":1,"head":"{{Other}}","rounds":[]}""");

        var result = await rig.Freeze().Run(ShipRig.Context("step.ship.freeze", facts), Token);

        Assert.Equal("stale", result.Result!.Outcome.Value);
        var finding = Assert.Single(Out(result, "findings").AsArray())!;
        Assert.Equal("SHIP-SHA-001", (string?)finding["rule"]);
        Assert.Contains("review", (string?)finding["message"], StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Packets.Calls, static c => c.Verb == "freeze");
        Assert.False(result.Result.Outputs.ContainsKey(FactKey.From("fact:handoff")));
    }

    [Fact]
    public async Task A_green_gate_that_ran_on_another_head_is_stale()
    {
        var rig = new ShipRig();
        var facts = await Packed(rig, ShipRig.Green(Other));

        var result = await rig.Freeze().Run(ShipRig.Context("step.ship.freeze", facts), Token);

        Assert.Equal("stale", result.Result!.Outcome.Value);
        Assert.Contains("green", (string?)Out(result, "findings")[0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_disagreeing_sha_is_a_finding()
    {
        var rig = new ShipRig();
        var facts = await Packed(rig, ShipRig.Green(Other));
        _ = rig.Files.With(ShipRig.ProofDir, "proof.json", $$"""{"schemaVersion":1,"head":"{{Other}}","items":[]}""");

        var result = await rig.Freeze().Run(ShipRig.Context("step.ship.freeze", facts), Token);

        Assert.Equal("stale", result.Result!.Outcome.Value);
        Assert.NotEmpty(Out(result, "findings").AsArray());
    }

    [Fact]
    public async Task A_draft_that_changed_since_the_pack_stops_the_freeze_with_SND_LINE_001()
    {
        var rig = new ShipRig();
        var facts = await Packed(rig);
        facts["handoffDraft"] = facts["handoffDraft"].Replace("\"hash\":\"sha256:", "\"hash\":\"sha256:0", StringComparison.Ordinal);

        var result = await rig.Freeze().Run(ShipRig.Context("step.ship.freeze", facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("SND-LINE-001", result.Failure, StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Packets.Calls, static c => c.Verb == "freeze");
    }

    [Fact]
    public async Task A_hand_off_that_aw_packets_refuses_to_freeze_parks_the_Step_with_its_failures()
    {
        var rig = new ShipRig();
        var facts = await Packed(rig);
        _ = rig.Packets.Fails(rig.Area.PathFor(ShipRig.Address, "handoff-packet").Value, new PacketFailure("PKT-HND-001", "green.json", "missing", "write it"));

        var result = await rig.Freeze().Run(ShipRig.Context("step.ship.freeze", facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("PKT-HND-001", result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("handoffDraft")]
    [InlineData("proofPack")]
    [InlineData("review")]
    public async Task A_missing_input_fact_parks_the_Step_and_names_it(string missing)
    {
        var rig = new ShipRig();
        var facts = await Packed(rig);
        _ = facts.Remove(missing);

        var result = await rig.Freeze().Run(ShipRig.Context("step.ship.freeze", facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("fact:" + missing, result.Failure, StringComparison.Ordinal);
    }
}
