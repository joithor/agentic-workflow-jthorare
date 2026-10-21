using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Sessions;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Ship;

/// <summary>The ship station's inputs as the prove and review stations leave them, and the Steps over fakes.</summary>
internal sealed class ShipRig
{
    internal const string Head = "abcabcabcabcabcabcabcabcabcabcabcabcabca";
    internal const string Address = "T-1/ship/1";
    internal const string PhaseDir = "/packets/p1";
    internal const string ProofDir = "/run/proof";
    internal const string ReviewDir = "/run/review";
    internal static readonly string PhaseHash = InMemoryPackets.HashOf(new PacketRef(PacketKind.Phase, PhaseDir));
    internal static readonly string ProofHash = InMemoryPackets.HashOf(new PacketRef(PacketKind.Proof, ProofDir));
    internal static readonly string ReviewHash = InMemoryPackets.HashOf(new PacketRef(PacketKind.Review, ReviewDir));
    internal const string ProofAcceptance = "sha256:4444444444444444444444444444444444444444444444444444444444444444";

    internal DirectoryFiles Files { get; } = new DirectoryFiles()
        .With(PhaseDir, "packet.json", """{"schemaVersion":1,"kind":"phase","id":"fix-line-p1","repo":"owner/name","baseSha":"1111111111111111111111111111111111111111","status":"frozen","hash":null,"links":{}}""")
        .With(PhaseDir, "phase.json", """{"goal":"Print results as JSON","kind":"feature","testChange":[{"path":"test/format.test.js","reason":"new format"}]}""")
        .With(ProofDir, "proof.json", $$"""{"schemaVersion":1,"head":"{{Head}}","items":[],"notRun":[]}""")
        .With(ProofDir, "index.json", $$$"""{"schemaVersion":1,"kind":"proof","parents":["{{{PhaseHash}}}"],"acceptance":{"self":"{{{ProofAcceptance}}}","extends":"sha256:aa"}}""")
        .With(ProofDir, "acceptance.json", $$"""{"schemaVersion":1,"parent":"sha256:aa","items":[{"id":"AC-1","text":"tally --json prints an array","status":"passing","evidence":[{"kind":"test","path":"evidence/a.txt","sha256":"sha256:{{new string('a', 64)}}","commit":"{{Head}}","at":"t"}]}]}""")
        .With(ProofDir, "holdout.json", $$"""{"schemaVersion":1,"commit":"{{Head}}","results":[{"scenario":"H1","item":"AC-1","verdict":"pass","spent":true},{"scenario":"H2","item":"AC-1","verdict":"pass","spent":false}]}""")
        .With(ProofDir, "not-proven.md", "# Not proven\n\n- AC-1 / SG-1b (test): the empty-file case\n")
        .With(ReviewDir, "review.json", $$"""{"schemaVersion":1,"head":"{{Head}}","verdict":"clean","rounds":[{"findings":[]},{"findings":[]}]}""");

    internal InMemoryRunArea Area { get; } = new();

    internal InMemoryPackets Packets { get; } = new();

    internal InMemoryRepoCheckouts Checkouts { get; } = new InMemoryRepoCheckouts().With("name", "/checkouts/name");

    internal ShipPackStep Pack() => new(Files, Area, Packets, Checkouts, new ReplacingScrubber("SECRET", "[redacted]"));

    internal ShipFreezeStep Freeze() => new(Files, Area, Packets, Checkouts);

    internal static string Green(string sha = Head) => $$"""{"gate":"local","outcome":"passed","sha":"{{sha}}","commands":[{"cmd":"dotnet test","exit":0}]}""";

    internal static Dictionary<string, string> PackFacts(string? lineContext = null, string? green = null) => new(StringComparer.Ordinal)
    {
        ["phasePacket"] = $$"""{"hash":"{{PhaseHash}}","dir":"{{PhaseDir}}"}""",
        ["proofPack"] = $$"""{"hash":"{{ProofHash}}","dir":"{{ProofDir}}"}""",
        ["review"] = $$"""{"hash":"{{ReviewHash}}","dir":"{{ReviewDir}}"}""",
        ["lineContext"] = lineContext ?? """{"line":"fix-line","phase":"p1","attempt":1,"base":"1111111111111111111111111111111111111111"}""",
        ["green"] = green ?? Green(),
    };

    internal static StepContext Context(string step, Dictionary<string, string> facts) =>
        BuildShipContexts.Of(step, facts) with { Address = Address, Repo = RepoName.From("name") };

    internal static Dictionary<string, string> FreezeFacts(string draft) => new(StringComparer.Ordinal)
    {
        ["handoffDraft"] = draft,
        ["proofPack"] = $$"""{"hash":"{{ProofHash}}","dir":"{{ProofDir}}"}""",
        ["review"] = $$"""{"hash":"{{ReviewHash}}","dir":"{{ReviewDir}}"}""",
    };
}
