using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Extract;

/// <summary>Gives every product file of a repo exactly one home feature, chosen by directory and never by a model (spec 2026-10-27 §2).</summary>
public static class HomeFeatures
{
    /// <summary>The most files a home feature holds before its directory is split one level.</summary>
    public const int SizeCap = 150;

    private const int MaxKeyLength = 200;
    private const int LabelFiles = SizeCap;
    private static readonly string[] _outputDirectories = ["node_modules", "dist", "build", "coverage", ".git", "bin", "obj"];

    /// <summary>Assigns the home features of a repo's files.</summary>
    /// <param name="repo">The repo: its name joins the keys, its deny, demote and role globs say which files get no home.</param>
    /// <param name="paths">The repo's full path list (before the readable-file filter, so a '.csproj' can mark a module root).</param>
    /// <param name="extracted">The deterministic extraction: a surface is 'part-of' the home feature of its defining file; null claims none.</param>
    /// <returns>The features, their links and the directory-to-feature lookup.</returns>
    public static HomeAssignment Assign(ContextRepo repo, IReadOnlyList<string> paths, ExtractedMap? extracted = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(paths);
        var all = paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var modules = all.Where(IsModuleMarker).Select(DirectoryOf).Where(d => !InOutput(d)).ToHashSet(StringComparer.Ordinal);
        var slots = new List<Slot>();
        foreach (var module in all.Where(p => IsHomed(repo, p)).GroupBy(p => ModuleOf(p, modules), StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var root = new Dir(module.Key);
            foreach (var file in module)
            {
                root.Add(module.Key.Length == 0 ? file : file[(module.Key.Length + 1)..], file);
            }

            Plan(root, isModuleRoot: true, parent: null, slots);
        }

        return Build(repo, slots, extracted);
    }

    private static void Plan(Dir node, bool isModuleRoot, Slot? parent, List<Slot> slots)
    {
        if (node.Total == 0)
        {
            return;
        }

        if (node.Total <= SizeCap || node.Children.Count == 0)
        {
            slots.Add(new Slot(node.Path, loose: false, group: false, node.AllFiles(), parent));
            return;
        }

        var group = new Slot(node.Path, loose: false, group: true, [], parent);
        slots.Add(group);
        if (isModuleRoot && node.Children.TryGetValue("src", out var src) && node.Total - src.Total <= SizeCap)
        {
            var rest = node.Loose.Concat(node.Children.Where(c => c.Key != "src").SelectMany(c => c.Value.AllFiles())).Order(StringComparer.Ordinal).ToList();
            if (rest.Count > 0)
            {
                slots.Add(new Slot(node.Path, loose: true, group: false, rest, group));
            }

            Plan(src, isModuleRoot: false, group, slots);
            return;
        }

        if (node.Loose.Count > 0)
        {
            slots.Add(new Slot(node.Path, loose: true, group: false, [.. node.Loose.Order(StringComparer.Ordinal)], group));
        }

        foreach (var child in node.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            Plan(child.Value, isModuleRoot: false, group, slots);
        }
    }

    private static HomeAssignment Build(ContextRepo repo, List<Slot> slots, ExtractedMap? extracted)
    {
        var keys = Keys(repo.Name, slots);
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var files = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        var directories = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        var requests = new List<FileCluster>();
        var requested = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        foreach (var slot in slots)
        {
            var key = keys[slot];
            nodes.Add(new GraphNode(key, LabelOf(repo.Name, slot), Provenance.Deterministic));
            if (!slot.Loose)
            {
                directories[slot.Path] = key;
            }

            if (slot.Parent is { } parent)
            {
                edges.Add(new GraphEdge(key, keys[parent], EdgeType.PartOf, Provenance.Deterministic));
            }

            if (slot.Files.Count == 0)
            {
                continue;
            }

            var id = ClusterId.From("h" + requests.Count.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));
            requests.Add(new FileCluster(id, [.. slot.Files.Take(LabelFiles)], 1.0));
            requested[id.Value] = key;
            foreach (var file in slot.Files)
            {
                files[file] = key;
                nodes.Add(new GraphNode(NodeKey.From("code:" + file), file[(file.LastIndexOf('/') + 1)..], Provenance.Deterministic));
                edges.Add(HomeEdge(repo.Name, file, key));
            }
        }

        edges.AddRange(Claims(extracted, files));
        return new HomeAssignment(ExtractedMap.Empty.Plus(new ExtractedMap(nodes, edges)), directories, files, requests, requested);
    }

    private static GraphEdge HomeEdge(ContextRepoName repo, string file, NodeKey home)
    {
        var evidence = RecordKey.TryFrom($"repo:{repo.Value}/{file}", out var record) ? new[] { new EvidenceRef(record) } : null;
        return new GraphEdge(NodeKey.From("code:" + file), home, EdgeType.Implements, Provenance.Deterministic, 1.0, evidence);
    }

    // A surface is part of the home feature of the file that defines it (the first one in path order when two files do).
    private static IEnumerable<GraphEdge> Claims(ExtractedMap? extracted, Dictionary<string, NodeKey> files)
    {
        if (extracted is null)
        {
            yield break;
        }

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var defining = extracted.Edges.Where(e => e is { Type: EdgeType.Implements, From.Kind: NodeKind.Code, To.Kind: NodeKind.Surface })
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal);
        foreach (var edge in defining)
        {
            if (files.TryGetValue(edge.From.Value["code:".Length..], out var home) && claimed.Add(edge.To.Value))
            {
                yield return new GraphEdge(edge.To, home, EdgeType.PartOf, Provenance.Deterministic);
            }
        }
    }

    // Every key comes from the repo name and the path alone. A key two paths share once the key function has lowercased it gets a hash of its path on every holder, so a new sibling never renames an old key.
    private static Dictionary<Slot, NodeKey> Keys(ContextRepoName repo, List<Slot> slots)
    {
        var bases = slots.ToDictionary(s => s, s => Fit(Base(repo, s), s, 8));
        var shared = slots.GroupBy(s => Normal(bases[s]), StringComparer.Ordinal).Where(g => g.Skip(1).Any()).SelectMany(g => g).ToHashSet();
        return slots.ToDictionary(s => s, s => KeyOf(shared.Contains(s) ? Fit(Base(repo, s) + "-" + Hash(s.Identity, 6), s, 8) : bases[s]));
    }

    private static string Normal(string raw) => KeyOf(raw).Value;

    private static NodeKey KeyOf(string raw) => EntryKey.For(NodeKey.From(raw));

    private static string Base(ContextRepoName repo, Slot slot) =>
        "feature:" + Escape(repo.Value) + (slot.Path.Length > 0 ? "." + string.Join('.', slot.Path.Split('/').Select(Escape)) : "") + (slot.Loose ? "._" : "");

    // A '_' in a name is written '__' and a '.' '_', so a/b.c, a/b_c and a.b/c differ and no escaped name ends in a lone '_'.
    private static string Escape(string name) => name.Replace("_", "__", StringComparison.Ordinal).Replace('.', '_');

    private static string Fit(string raw, Slot slot, int hex)
    {
        if (raw.Length <= MaxKeyLength)
        {
            return raw;
        }

        var cut = MaxKeyLength - 1 - hex;
        if (char.IsHighSurrogate(raw[cut - 1]))
        {
            cut--;
        }

        return raw[..cut] + "-" + Hash(slot.Identity, hex);
    }

    private static string Hash(string text, int length) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..length];

    private static string LabelOf(ContextRepoName repo, Slot slot)
    {
        var name = slot.Path.Length == 0 ? repo.Value : slot.Path[(slot.Path.LastIndexOf('/') + 1)..];
        var label = ClusterFeatures.Humanize(name);
        return slot.Loose ? label + " (loose files)" : label;
    }

    private static bool IsHomed(ContextRepo repo, string path) =>
        Recognizers.MayRead(path)
        && !path.EndsWith(".json", StringComparison.Ordinal)
        && !InOutput(path)
        && !ManifestFiles.Is(path)
        && !repo.Denies(path)
        && !repo.Demotes(path)
        && NodeKey.TryFrom("code:" + path, out _);

    private static bool InOutput(string path) => path.Split('/').Any(s => _outputDirectories.Contains(s, StringComparer.Ordinal));

    private static bool IsModuleMarker(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return string.Equals(name, "package.json", StringComparison.Ordinal) || name.EndsWith(".csproj", StringComparison.Ordinal);
    }

    private static string DirectoryOf(string path) => path.Contains('/', StringComparison.Ordinal) ? path[..path.LastIndexOf('/')] : "";

    private static string ModuleOf(string path, HashSet<string> modules)
    {
        var dir = DirectoryOf(path);
        while (dir.Length > 0 && !modules.Contains(dir))
        {
            dir = DirectoryOf(dir);
        }

        return dir;
    }

    private sealed class Slot(string path, bool loose, bool group, List<string> files, Slot? parent)
    {
        public string Path { get; } = path;

        public bool Loose { get; } = loose;

        public bool Group { get; } = group;

        public List<string> Files { get; } = files;

        public Slot? Parent { get; } = parent;

        public string Identity => Loose ? Path + "\0loose" : Path;
    }

    private sealed class Dir(string path)
    {
        public string Path { get; } = path;

        public List<string> Loose { get; } = [];

        public Dictionary<string, Dir> Children { get; } = new(StringComparer.Ordinal);

        public int Total { get; private set; }

        public void Add(string relative, string file)
        {
            Total++;
            var slash = relative.IndexOf('/', StringComparison.Ordinal);
            if (slash < 0)
            {
                Loose.Add(file);
                return;
            }

            var name = relative[..slash];
            if (!Children.TryGetValue(name, out var child))
            {
                child = new Dir(Path.Length == 0 ? name : Path + "/" + name);
                Children[name] = child;
            }

            child.Add(relative[(slash + 1)..], file);
        }

        public List<string> AllFiles()
        {
            var files = new List<string>(Total);
            Collect(files);
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        private void Collect(List<string> into)
        {
            into.AddRange(Loose);
            foreach (var child in Children.Values)
            {
                child.Collect(into);
            }
        }
    }
}
