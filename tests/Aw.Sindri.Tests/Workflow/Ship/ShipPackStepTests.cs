using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Packets;

namespace Aw.Sindri.Tests.Workflow.Ship;

/// <summary>'step.ship.pack' on the block runtime (line spec §7.5): the hand-off draft in the run area, with the pr.json and body.md the forge's PR-open reads.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipPackStepTests
{
    private const string DraftDir = "handoff-packet";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> Written(ShipRig rig, string name) => (await rig.Area.Read(ShipRig.Address, $"{DraftDir}/{name}", Token)).Value;

    private static async Task<JsonNode> Json(ShipRig rig, string name) => JsonNode.Parse(await Written(rig, name))!;

    [Fact]
    public async Task A_proven_reviewed_head_packs_the_five_files_of_the_hand_off_into_the_run_area()
    {
        var rig = new ShipRig();

        var result = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts()), Token);

        Assert.Equal("packed", result.Result!.Outcome.Value);
        foreach (var name in new[] { "packet.json", "pr.json", "body.md", "green.json", "index.json" })
        {
            Assert.NotEmpty(await Written(rig, name));
        }

        var packet = await Json(rig, "packet.json");
        Assert.Equal(("handoff", "owner/name", "draft", "fix-line-p1-handoff"), ((string?)packet["kind"], (string?)packet["repo"], (string?)packet["status"], (string?)packet["id"]));
        Assert.Equal(["pr", "body", "green", "index"], ((JsonObject)packet["links"]!).Select(static l => l.Key).ToArray());
        var dir = rig.Area.PathFor(ShipRig.Address, DraftDir).Value;
        var draft = JsonNode.Parse(result.Result.Outputs[FactKey.From("fact:handoffDraft")].GetRawText())!;
        Assert.Equal((InMemoryPackets.HashOf(new PacketRef(PacketKind.Handoff, dir)), dir), ((string?)draft["hash"], (string?)draft["dir"]));
        Assert.Contains(rig.Packets.Calls, static c => c is { Verb: "validate", Packet.Kind: PacketKind.Handoff });
    }

    [Fact]
    public async Task The_pr_file_names_the_base_branch_head_title_and_labels_the_forge_opens_the_pull_request_with()
    {
        var rig = new ShipRig();

        _ = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts()), Token);

        var pr = await Json(rig, "pr.json");
        Assert.Equal(("main", "sindri/fix-line/p1", ShipRig.Head, "feat: Print results as JSON"), ((string?)pr["base"], (string?)pr["headBranch"], (string?)pr["headSha"], (string?)pr["title"]));
        Assert.Null(pr["stackParent"]);
        Assert.Equal(1, (int?)pr["schemaVersion"]);
        Assert.Equal(["test-change"], pr["labels"]!.AsArray().Select(static l => (string?)l).ToArray());
    }

    [Fact]
    public async Task A_stacked_phase_opens_against_its_parent_branch_and_a_phase_without_a_test_change_has_no_label()
    {
        var rig = new ShipRig();
        _ = rig.Files.With(ShipRig.PhaseDir, "phase.json", """{"goal":"Fix it","kind":"fix","testChange":[]}""");
        var line = """{"line":"fix-line","phase":"p2","base":"1111111111111111111111111111111111111111","stackParent":"sindri/fix-line/p1"}""";

        _ = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts(line)), Token);

        var pr = await Json(rig, "pr.json");
        Assert.Equal(("sindri/fix-line/p1", "sindri/fix-line/p1", "fix: Fix it", "sindri/fix-line/p2"), ((string?)pr["base"], (string?)pr["stackParent"], (string?)pr["title"], (string?)pr["headBranch"]));
        Assert.Empty(pr["labels"]!.AsArray());
    }

    [Fact]
    public async Task The_body_is_generated_from_the_packets_with_the_acceptance_evidence_hidden_counts_not_proven_and_review_rounds()
    {
        var rig = new ShipRig();

        _ = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts()), Token);

        var body = await Written(rig, "body.md");
        Assert.Contains("Print results as JSON", body, StringComparison.Ordinal);
        Assert.Contains("AC-1", body, StringComparison.Ordinal);
        Assert.Contains("evidence/a.txt", body, StringComparison.Ordinal);
        Assert.Contains(new string('a', 64), body, StringComparison.Ordinal);
        Assert.Contains(ShipRig.Head, body, StringComparison.Ordinal);
        Assert.Contains("2 of 2", body, StringComparison.Ordinal);
        Assert.Contains("the empty-file case", body, StringComparison.Ordinal);
        Assert.Contains("2 rounds", body, StringComparison.Ordinal);
        Assert.Contains(ShipRig.Address, body, StringComparison.Ordinal);
        Assert.Contains(ShipRig.ProofHash, body, StringComparison.Ordinal);
        Assert.DoesNotContain("H1", body, StringComparison.Ordinal);   // hidden scenarios show counts and verdicts, never an id or text
    }

    [Fact]
    public async Task The_title_and_the_body_pass_the_scrubber_before_they_are_written()
    {
        var rig = new ShipRig();
        _ = rig.Files.With(ShipRig.PhaseDir, "phase.json", """{"goal":"Hide the SECRET","kind":"feature","testChange":[]}""");

        _ = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts()), Token);

        Assert.Equal("feat: Hide the [redacted]", (string?)(await Json(rig, "pr.json"))["title"]);
        var body = await Written(rig, "body.md");
        Assert.DoesNotContain("SECRET", body, StringComparison.Ordinal);
        Assert.Contains("[redacted]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_green_gate_and_the_parents_are_recorded_in_the_hand_off()
    {
        var rig = new ShipRig();
        var line = """{"line":"fix-line","phase":"p1","base":"1111111111111111111111111111111111111111","packets":{"scope":"sha256:s","design":"sha256:d","plan":"sha256:p"}}""";

        _ = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts(line)), Token);

        var green = await Json(rig, "green.json");
        Assert.Equal((1, "local", "passed", ShipRig.Head), ((int?)green["schemaVersion"], (string?)green["gate"], (string?)green["outcome"], (string?)green["sha"]));
        var index = await Json(rig, "index.json");
        Assert.Equal(["sha256:s", "sha256:d", "sha256:p", ShipRig.PhaseHash, ShipRig.ProofHash, ShipRig.ReviewHash], index["parents"]!.AsArray().Select(static p => (string?)p).ToArray());
        Assert.Equal(("handoff", null, ShipRig.ProofAcceptance), ((string?)index["kind"], (string?)index["acceptance"]!["self"], (string?)index["acceptance"]!["extends"]));
    }

    [Fact]
    public async Task A_packet_whose_hash_is_not_the_one_the_fact_names_stops_the_pack_with_SND_LINE_001()
    {
        var rig = new ShipRig();
        var facts = ShipRig.PackFacts();
        facts["proofPack"] = $$"""{"hash":"sha256:{{new string('9', 64)}}","dir":"{{ShipRig.ProofDir}}"}""";

        var result = await rig.Pack().Run(ShipRig.Context("step.ship.pack", facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("SND-LINE-001", result.Failure, StringComparison.Ordinal);
        Assert.Equal(string.Empty, await Written(rig, "pr.json"));
    }

    [Fact]
    public async Task A_draft_that_aw_packets_refuses_parks_the_Step_with_its_failures()
    {
        var rig = new ShipRig();
        _ = rig.Packets.Fails(rig.Area.PathFor(ShipRig.Address, DraftDir).Value, new PacketFailure("PKT-HND-007", "pr.json", "headSha is not a full sha", "use the full sha"));

        var result = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts()), Token);

        Assert.Null(result.Result);
        Assert.Contains("PKT-HND-007", result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("phasePacket")]
    [InlineData("proofPack")]
    [InlineData("review")]
    [InlineData("lineContext")]
    [InlineData("green")]
    public async Task A_missing_input_fact_parks_the_Step_and_names_it(string missing)
    {
        var rig = new ShipRig();
        var facts = ShipRig.PackFacts();
        _ = facts.Remove(missing);

        var result = await rig.Pack().Run(ShipRig.Context("step.ship.pack", facts), Token);

        Assert.Null(result.Result);
        Assert.Contains("fact:" + missing, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_proof_pack_without_a_head_parks_the_Step()
    {
        var rig = new ShipRig();
        _ = rig.Files.With(ShipRig.ProofDir, "proof.json", """{"schemaVersion":1,"items":[]}""");

        var result = await rig.Pack().Run(ShipRig.Context("step.ship.pack", ShipRig.PackFacts()), Token);

        Assert.Null(result.Result);
        Assert.Contains("proof.json", result.Failure, StringComparison.Ordinal);
    }
}
