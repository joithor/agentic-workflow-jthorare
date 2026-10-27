using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>One read of the store for one build: the nodes and edges it finds and the counts the report prints. Not thread-safe; a build makes one per read.</summary>
internal sealed class RecordPass(IRecordStore records, MapRecordInput input, IReadOnlyList<StoredRecord> stored)
{
    private static readonly RecordLinkKind[] _linkKinds = [RecordLinkKind.Parent, RecordLinkKind.Blocks, RecordLinkKind.Duplicates, RecordLinkKind.Relates, RecordLinkKind.Closes, RecordLinkKind.Attachment];

    private readonly RepoPaths _paths = new(input.Repos);
    private readonly Dictionary<string, StoredRecord> _live = stored.ToDictionary(r => r.Key.Value, StringComparer.Ordinal);
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string From, string To), GraphEdge> _edges = [];
    private readonly Dictionary<CitationOutcome, int> _cited = [];
    private int _notSynced;
    private int _older;
    private int _removedTouches;
    private int _overCap;
    private int _cut;

    /// <summary>Reads the docs, then the tracker records.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The store's error when it can't be read, else null.</returns>
    internal async ValueTask<Error?> Run(CancellationToken cancellationToken)
    {
        foreach (var repo in input.Repos)
        {
            if (await Docs(repo, cancellationToken).ConfigureAwait(false) is { } failed)
            {
                return failed;
            }
        }

        foreach (var record in stored.Where(r => r.Source != RepoDocs.Id && r.Kind != RecordKind.Doc).OrderBy(r => r.Key.Value, StringComparer.Ordinal))
        {
            if (await Tracker(record, cancellationToken).ConfigureAwait(false) is { } failed)
            {
                return failed;
            }
        }

        return null;
    }

    /// <summary>What the pass found.</summary>
    /// <param name="stamp">The store's record stamp, read before the pass.</param>
    /// <returns>The map part and the report lines.</returns>
    internal MapRecordReading Reading(long stamp)
    {
        var map = new ExtractedMap(
            [.. _nodes.Values.OrderBy(n => n.Key.Value, StringComparer.Ordinal)],
            [.. _edges.Values.OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal)]);
        return new MapRecordReading(map, stamp, Notes(stamp));
    }

    private async ValueTask<Error?> Docs(MapRepoFiles repo, CancellationToken cancellationToken)
    {
        foreach (var node in DocRecords.Find(repo.Name, repo.Paths, input.DocGlobs, input.DirectoryNames, input.At))
        {
            _nodes[node.Key.Value] = node;
            if (!_live.TryGetValue(RepoDocs.Key(repo.Name, node.Label).Value, out var doc))
            {
                _notSynced++;
            }
            else if (!doc.Blocked && !(repo.Blobs.TryGetValue(node.Label, out var blob) && string.Equals(doc.Version.Value, blob, StringComparison.Ordinal)))
            {
                _older++;
            }
            else if (!doc.Blocked && await Cite(doc, node.Key, cancellationToken).ConfigureAwait(false) is { Error: { } failed })
            {
                return failed;
            }
        }

        return null;
    }

    private async ValueTask<Error?> Tracker(StoredRecord record, CancellationToken cancellationToken)
    {
        if (!NodeKey.TryFrom("record:" + record.Key.Value, out var from))
        {
            return null;
        }

        var made = Links(record, from) + Touches(record, from);
        if (!record.Blocked)
        {
            var cited = await Cite(record, from, cancellationToken).ConfigureAwait(false);
            if (cited.Error is { } failed)
            {
                return failed;
            }

            made += cited.Made;
        }

        if (made > 0)
        {
            _ = _nodes.TryAdd(from.Value, Node(record, from));
        }

        return null;
    }

    // The paths a record's text cites, resolved against the path lists: an exact match is an edge, anything else a count.
    private async ValueTask<(int Made, Error? Error)> Cite(StoredRecord record, NodeKey from, CancellationToken cancellationToken)
    {
        var chunks = await records.Chunks(record.Key, cancellationToken).ConfigureAwait(false);
        if (chunks.IsError)
        {
            return (0, chunks.Error);
        }

        var made = 0;
        foreach (var path in CitedPaths.Find(string.Join('\n', chunks.Value.Select(c => c.Text))))
        {
            var outcome = _paths.Resolve(record.Repo, path);
            _cited[outcome] = _cited.GetValueOrDefault(outcome) + 1;
            made += outcome == CitationOutcome.Resolved && AddCode(from, path, record.Key) ? 1 : 0;
        }

        return (made, null);
    }

    // Source-stated links between two stored records; a target the store doesn't hold, or a doc outside the path list, makes no edge.
    private int Links(StoredRecord record, NodeKey from)
    {
        var made = 0;
        foreach (var link in record.Links.Where(l => _linkKinds.Contains(l.Kind)))
        {
            if (link.ToKey is { } linked && _live.TryGetValue(linked.Value, out var target) && Target(target) is { } to)
            {
                made += AddEdge(new GraphEdge(from, to, EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(record.Key, link.Kind.ToString().ToLowerInvariant())])) ? 1 : 0;
            }
        }

        return made;
    }

    private NodeKey? Target(StoredRecord target)
    {
        if (!NodeKey.TryFrom("record:" + target.Key.Value, out var key))
        {
            return null;
        }

        if (target.Source == RepoDocs.Id)
        {
            return _nodes.ContainsKey(key.Value) ? key : null;
        }

        _ = _nodes.TryAdd(key.Value, Node(target, key));
        return key;
    }

    // A pull request's changed files: those in the path list, the first TouchCap of them in path order; a path since removed is a count of its own.
    private int Touches(StoredRecord record, NodeKey from)
    {
        var resolved = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var touched in record.Links.Where(l => l.Kind == RecordLinkKind.Touches).Select(l => _paths.Touched(l.ToRef)).OfType<(string Repo, string Path)>())
        {
            if (_paths.Has(touched.Repo, touched.Path))
            {
                _ = resolved.Add(touched.Path);
            }
            else
            {
                _removedTouches++;
            }
        }

        if (resolved.Count > MapRecordSources.TouchCap)
        {
            _overCap++;
            _cut += resolved.Count - MapRecordSources.TouchCap;
        }

        return resolved.Take(MapRecordSources.TouchCap).Count(path => AddCode(from, path, record.Key));
    }

    private bool AddCode(NodeKey from, string path, RecordKey evidence) =>
        NodeKey.TryFrom("code:" + path, out var code) && AddEdge(new GraphEdge(from, code, EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(evidence)]));

    private bool AddEdge(GraphEdge edge) => _edges.TryAdd((edge.From.Value, edge.To.Value), edge);

    private static GraphNode Node(StoredRecord record, NodeKey key) => new(key, record.Title, Provenance.Deterministic, At: record.UpdatedAt);

    private List<string> Notes(long stamp)
    {
        var notes = new List<string> { Line($"records: as of stamp {stamp}") };
        AddIf(notes, _notSynced, n => Line($"records: {n} docs not yet synced"));
        AddIf(notes, _older, n => Line($"records: {n} docs older than HEAD"));
        var resolved = _cited.GetValueOrDefault(CitationOutcome.Resolved);
        var missing = _cited.GetValueOrDefault(CitationOutcome.Missing);
        var other = _cited.GetValueOrDefault(CitationOutcome.OtherRepo);
        var ambiguous = _cited.GetValueOrDefault(CitationOutcome.Ambiguous);
        AddIf(notes, resolved + missing + other + ambiguous, _ => Line($"citations: {resolved} resolved, {missing + other + ambiguous} unresolved ({missing} missing, {other} other repo, {ambiguous} ambiguous)"));
        AddIf(notes, _removedTouches, n => Line($"touched files since removed: {n}"));
        AddIf(notes, _overCap, n => Line($"touches: {n} PRs over the cap ({_cut} paths cut)"));
        return notes;
    }

    private static void AddIf(List<string> notes, int count, Func<int, string> line)
    {
        if (count > 0)
        {
            notes.Add(line(count));
        }
    }

    private static string Line(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
