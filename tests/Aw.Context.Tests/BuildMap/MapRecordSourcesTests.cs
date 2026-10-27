using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.BuildMap;
using Aw.Context.Testing.Records;
using Aw.Context.Testing.Symbols;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Spec 2026-10-27 §7.1 and §7.5: the build reads docs and tracker records from the store; cited paths and PR files become edges, what doesn't resolve is a number in the report.</summary>
public sealed class MapRecordSourcesTests
{
    private static readonly ContextRepoName _app = ContextRepoName.From("app");
    private static readonly ContextRepoName _lib = ContextRepoName.From("lib");
    private static readonly string[] _docGlobs = ["docs/**/*.md"];
    private static readonly string[] _names = ["AGENTS.md"];
    private static readonly string[] _appPaths = ["docs/guide.md", "src/billing/AGENTS.md", "src/billing/Invoice.cs", "src/billing/Tax.cs", "src/common/Both.cs"];
    private static readonly string[] _libPaths = ["src/common/Both.cs", "src/shared/Util.cs"];

    private static RecordWrite Doc(string path, string version, params string[] texts) =>
        RecordSamples.Write(RecordSamples.Record(RepoDocs.Key(_app, path).Value, version, RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _app, Title = path }, texts);

    private static RecordWrite Tracker(string key, string text, RecordKind kind = RecordKind.Issue, ContextRepoName? repo = null, params RecordLink[] links) =>
        RecordSamples.Write(RecordSamples.Record(key, "v1", RecordSamples.Github) with { Kind = kind, Repo = repo, Links = links }, text);

    private static async Task<FakeRecordStore> Store(params RecordWrite[] writes)
    {
        var store = new FakeRecordStore();
        foreach (var source in writes.GroupBy(w => w.Record.Source))
        {
            _ = await store.Commit(RecordSamples.Batch(source.Key, 1, [.. source]), CancellationToken.None);
        }

        return store;
    }

    private static MapRecordInput Input(bool lib = false) => new(
        [new MapRepoFiles(_app, _appPaths, new Dictionary<string, string>(StringComparer.Ordinal) { ["docs/guide.md"] = "b1", ["src/billing/AGENTS.md"] = "b2" }), .. lib ? [new MapRepoFiles(_lib, _libPaths, new Dictionary<string, string>(StringComparer.Ordinal))] : Array.Empty<MapRepoFiles>()],
        _docGlobs, _names, RecordSamples.Now);

    private static async Task<MapRecordReading> Read(FakeRecordStore store, bool lib = false)
    {
        var result = await new MapRecordSources(store, new FakeCodeSymbolSource([])).Read(Input(lib), CancellationToken.None);
        return result.Value;
    }

    private static void AssertNote(MapRecordReading reading, string note) => Assert.Contains(reading.Notes, n => string.Equals(n, note, StringComparison.Ordinal));

    private static string Doc1(string path) => "record:" + RepoDocs.Key(_app, path).Value;

    private static List<string> Targets(MapRecordReading reading, string from) =>
        [.. reading.Map.Edges.Where(e => e.From.Value == from && e.Type == EdgeType.Mentions).Select(e => e.To.Value).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_in_the_path_list_is_a_node_even_when_the_store_has_no_row_for_it_and_the_report_counts_it()
    {
        var reading = await Read(await Store(Doc("src/billing/AGENTS.md", "b2", "no paths here")));

        Assert.Equal([Doc1("docs/guide.md"), Doc1("src/billing/AGENTS.md")], reading.Map.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        AssertNote(reading, "records: 1 docs not yet synced");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fresh_doc_cites_files_as_deterministic_mentions_at_full_confidence_and_the_unresolved_are_counted()
    {
        var reading = await Read(await Store(Doc("docs/guide.md", "b1", "Start at `src/billing/Invoice.cs`", "then src/billing/Tax.cs:12 and `src/billing/Gone.cs`.")));

        Assert.Equal(["code:src/billing/Invoice.cs", "code:src/billing/Tax.cs"], Targets(reading, Doc1("docs/guide.md")), StringComparer.Ordinal);
        Assert.All(reading.Map.Edges, e => Assert.Equal((Provenance.Deterministic, 1.0), (e.Provenance, e.Confidence)));
        Assert.Equal(RepoDocs.Key(_app, "docs/guide.md"), Assert.Single(reading.Map.Edges, e => e.To.Value == "code:src/billing/Tax.cs").EvidenceOrEmpty.Single().Key);
        AssertNote(reading, "citations: 2 resolved, 1 unresolved (1 missing, 0 other repo, 0 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_older_than_its_blob_at_head_keeps_its_node_but_its_citations_are_skipped_and_counted()
    {
        var reading = await Read(await Store(Doc("docs/guide.md", "old-blob", "see `src/billing/Invoice.cs`")));

        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == Doc1("docs/guide.md"));
        Assert.Empty(reading.Map.Edges);
        AssertNote(reading, "records: 1 docs older than HEAD");
        Assert.DoesNotContain(reading.Notes, n => n.StartsWith("citations:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_the_scrubber_withheld_keeps_its_node_with_no_edges_and_is_not_called_stale()
    {
        var blocked = RecordSamples.Record(RepoDocs.Key(_app, "docs/guide.md").Value, "b1", RepoDocs.Id, blocked: true) with { Kind = RecordKind.Doc, Repo = _app };
        var reading = await Read(await Store(new RecordWrite(blocked, [])));

        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == Doc1("docs/guide.md"));
        Assert.Empty(reading.Map.Edges);
        Assert.DoesNotContain(reading.Notes, n => n.Contains("older than HEAD", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tracker_record_with_no_edge_into_the_map_stays_in_the_store_and_is_not_a_node()
    {
        var reading = await Read(await Store(Tracker("github:issue/acme/app/1", "nothing to cite")));

        Assert.DoesNotContain(reading.Map.Nodes, n => n.Key.Value.Contains("github:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tracker_record_that_cites_a_file_becomes_a_node_keyed_by_its_record_key_with_a_mentions_edge()
    {
        var reading = await Read(await Store(Tracker("github:issue/acme/app/2", "breaks in `src/billing/Tax.cs`", repo: _app)));

        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == "record:github:issue/acme/app/2");
        Assert.Equal(["code:src/billing/Tax.cs"], Targets(reading, "record:github:issue/acme/app/2"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_citation_is_resolved_against_the_records_own_repo_then_counted_as_other_repo_or_ambiguous_never_guessed()
    {
        var store = await Store(
            Doc("docs/guide.md", "b1", "uses `src/shared/Util.cs`"),
            Tracker("github:issue/acme/app/3", "see `src/common/Both.cs` and `src/shared/Util.cs`"),
            Tracker("github:issue/acme/app/4", "see `src/common/Both.cs`", repo: _app));
        var reading = await Read(store, lib: true);

        Assert.Equal(["code:src/shared/Util.cs"], Targets(reading, "record:github:issue/acme/app/3"), StringComparer.Ordinal);
        Assert.Equal(["code:src/common/Both.cs"], Targets(reading, "record:github:issue/acme/app/4"), StringComparer.Ordinal);
        Assert.Empty(Targets(reading, Doc1("docs/guide.md")));
        AssertNote(reading, "citations: 2 resolved, 2 unresolved (0 missing, 1 other repo, 1 ambiguous)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_touches_the_files_it_changed_and_a_path_since_removed_is_its_own_count()
    {
        var pr = Tracker("github:pr/acme/app/7", "Fix tax", RecordKind.Pr, _app,
            new RecordLink("path:app/src/billing/Tax.cs", RecordLinkKind.Touches, Title: null),
            new RecordLink("path:app/src/billing/Invoice.cs", RecordLinkKind.Touches, Title: null),
            new RecordLink("path:app/src/billing/Deleted.cs", RecordLinkKind.Touches, Title: null));
        var reading = await Read(await Store(pr));

        Assert.Equal(["code:src/billing/Invoice.cs", "code:src/billing/Tax.cs"], Targets(reading, "record:github:pr/acme/app/7"), StringComparer.Ordinal);
        AssertNote(reading, "touched files since removed: 1");
        Assert.DoesNotContain(reading.Notes, n => n.StartsWith("citations:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_over_the_cap_keeps_the_first_fifty_paths_in_order_and_reports_the_rest_as_cut()
    {
        var paths = Enumerable.Range(0, 60).Select(i => string.Create(CultureInfo.InvariantCulture, $"src/gen/F{i:D2}.cs")).ToList();
        var pr = Tracker("github:pr/acme/app/8", "Big rename", RecordKind.Pr, _app, [.. paths.Select(p => new RecordLink("path:app/" + p, RecordLinkKind.Touches, Title: null))]);
        var input = new MapRecordInput([new MapRepoFiles(_app, paths, new Dictionary<string, string>(StringComparer.Ordinal))], _docGlobs, _names, RecordSamples.Now);
        var reading = (await new MapRecordSources(await Store(pr), new FakeCodeSymbolSource([])).Read(input, CancellationToken.None)).Value;

        Assert.Equal(MapRecordSources.TouchCap, Targets(reading, "record:github:pr/acme/app/8").Count);
        Assert.Equal(["code:src/gen/F00.cs", "code:src/gen/F49.cs"], [Targets(reading, "record:github:pr/acme/app/8")[0], Targets(reading, "record:github:pr/acme/app/8")[^1]], StringComparer.Ordinal);
        AssertNote(reading, "touches: 1 PRs over the cap (10 paths cut)");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_stated_link_between_two_stored_records_is_a_mentions_edge_carrying_the_link_kind_and_makes_both_ends_nodes()
    {
        var issue = Tracker("linear:issue/abc", "Tax is wrong");
        var pr = Tracker("github:pr/acme/app/9", "Fix tax", RecordKind.Pr, _app, new RecordLink("linear:issue/abc", RecordLinkKind.Closes, Title: null), new RecordLink("linear:issue/missing", RecordLinkKind.Relates, Title: null));
        var reading = await Read(await Store(issue, pr));

        var edge = Assert.Single(reading.Map.Edges, e => e.To.Value == "record:linear:issue/abc");
        Assert.Equal(("record:github:pr/acme/app/9", EdgeType.Mentions, Provenance.Deterministic), (edge.From.Value, edge.Type, edge.Provenance));
        Assert.Equal("closes", Assert.Single(edge.EvidenceOrEmpty).Span);
        Assert.Contains(reading.Map.Nodes, n => n.Key.Value == "record:linear:issue/abc");
        Assert.DoesNotContain(reading.Map.Nodes, n => n.Key.Value.Contains("missing", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_removed_record_is_neither_a_node_nor_a_source_of_edges()
    {
        var store = await Store(Tracker("github:issue/acme/app/5", "cites `src/billing/Tax.cs`", repo: _app));
        _ = await store.Commit(new RecordBatch(RecordSamples.Github, [], [RecordKey.From("github:issue/acme/app/5")], new SyncPosition(SourceCursor.From("2"), SyncPhase.Incremental, BackfillCursor: null), RecordSamples.Now), CancellationToken.None);
        var reading = await Read(store);

        Assert.DoesNotContain(reading.Map.Nodes, n => n.Key.Value.Contains("issue/acme/app/5", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_build_names_the_store_stamp_it_read_and_reads_the_same_nodes_and_edges_twice()
    {
        var store = await Store(Doc("docs/guide.md", "b1", "see `src/billing/Tax.cs`"), Tracker("github:issue/acme/app/6", "see `src/billing/Invoice.cs`", repo: _app));
        var stamp = (await store.Stamps(CancellationToken.None)).Value.RecordStamp;
        var first = await Read(store);
        var second = await Read(store);

        Assert.Equal(stamp, first.Stamp);
        AssertNote(first, string.Create(CultureInfo.InvariantCulture, $"records: as of stamp {stamp}"));
        Assert.Equal(first.Map.Nodes.Select(n => n.Key.Value), second.Map.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.Equal(first.Map.Edges.Select(e => (e.From.Value, e.To.Value)), second.Map.Edges.Select(e => (e.From.Value, e.To.Value)));
    }
}
