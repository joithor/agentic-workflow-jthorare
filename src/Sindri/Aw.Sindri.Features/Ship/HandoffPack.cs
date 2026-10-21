using System.Text.Json.Nodes;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.Ship;

/// <summary>The files of the hand-off packet (line spec §3.2 row 7): packet.json, pr.json, body.md, green.json and index.json, as pure functions of what the packets say. Title, labels and body pass the scrubber.</summary>
internal static class HandoffPack
{
    /// <summary>The label a phase that may change tests carries.</summary>
    internal const string TestChangeLabel = "test-change";

    /// <summary>The names of the files, in the order the packet's links list them.</summary>
    internal static IReadOnlyList<string> FileNames { get; } = ["packet.json", "pr.json", "body.md", "green.json", "index.json"];

    /// <summary>Every file as text, by name.</summary>
    /// <param name="inputs">What the packets and the line say.</param>
    /// <param name="scrubber">Takes secrets out of the title, labels and body.</param>
    /// <returns>File name to text.</returns>
    internal static IReadOnlyDictionary<string, string> Files(HandoffInputs inputs, ITextScrubber scrubber)
    {
        var (sources, line, _, parents, green) = inputs;
        var body = scrubber.Scrub(HandoffBody.Render(inputs));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["packet.json"] = Packet(sources).ToJsonString(),
            ["pr.json"] = Pr(sources, line, scrubber).ToJsonString(),
            ["body.md"] = body,
            ["green.json"] = Green(green).ToJsonString(),
            ["index.json"] = Index(sources, line, parents).ToJsonString(),
        };
    }

    private static JsonObject Packet(PackSources sources) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "handoff",
        ["id"] = sources.PhaseId + "-handoff",
        ["repo"] = sources.Repo,
        ["baseSha"] = sources.BaseSha,
        ["status"] = "draft",
        ["hash"] = null,
        ["links"] = new JsonObject { ["pr"] = "pr.json", ["body"] = "body.md", ["green"] = "green.json", ["index"] = "index.json" },
    };

    private static JsonObject Pr(PackSources sources, HandoffLine line, ITextScrubber scrubber) => new()
    {
        ["schemaVersion"] = 1,
        ["base"] = line.Base,
        ["headBranch"] = line.Branch,
        ["headSha"] = sources.Head,
        ["stackParent"] = line.StackParent,
        ["title"] = scrubber.Scrub(Title(sources)),
        ["labels"] = new JsonArray([.. (sources.TestChange ? new[] { TestChangeLabel } : []).Select(l => (JsonNode)JsonValue.Create(scrubber.Scrub(l)))]),
    };

    private static string Title(PackSources sources)
    {
        var goal = sources.Goal.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? sources.PhaseId;
        return $"{Type(sources.Kind)}: {(goal.Length > 72 ? goal[..72].TrimEnd() : goal)}";
    }

    private static string Type(string kind) => kind switch
    {
        "feature" => "feat",
        "fix" => "fix",
        "refactor" => "refactor",
        "docs" => "docs",
        _ => "chore",
    };

    // The gate's fact names the commit it ran on as 'sha' or 'head'; green.json carries it as 'sha'.
    private static JsonObject Green(JsonObject green)
    {
        var file = (JsonObject)green.DeepClone();
        var sha = ShipPacket.Text(file, "sha") ?? ShipPacket.Text(file, "head");
        _ = file.Remove("head");
        file["schemaVersion"] = 1;
        file["sha"] = sha;
        return file;
    }

    private static JsonObject Index(PackSources sources, HandoffLine line, IReadOnlyList<string> parents) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "handoff",
        ["parents"] = new JsonArray([.. line.Earlier.Concat(parents).Select(p => (JsonNode)JsonValue.Create(p))]),
        ["acceptance"] = new JsonObject { ["self"] = null, ["extends"] = sources.AcceptanceHash },
    };
}
