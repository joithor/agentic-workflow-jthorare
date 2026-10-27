using Aw.Context.Core.Engine;

namespace Aw.Context.Features.BuildMap;

/// <summary>The build's path lists, to resolve a path exactly: no basename match, no fuzzy match.</summary>
internal sealed class RepoPaths
{
    private readonly Dictionary<string, HashSet<string>> _byRepo;
    private readonly List<string> _longestFirst;

    internal RepoPaths(IReadOnlyList<MapRepoFiles> repos)
    {
        _byRepo = repos.ToDictionary(r => r.Name.Value, r => r.Paths.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        _longestFirst = [.. _byRepo.Keys.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal)];
    }

    /// <summary>Resolves a cited path.</summary>
    /// <param name="repo">The citing record's repo, or null for a record that names none.</param>
    /// <param name="path">The path.</param>
    /// <returns>How it fared.</returns>
    internal CitationOutcome Resolve(ContextRepoName? repo, string path)
    {
        var holders = _byRepo.Where(r => r.Value.Contains(path)).Select(r => r.Key).ToList();
        return (repo, holders.Count) switch
        {
            (_, 0) => CitationOutcome.Missing,
            ({ } own, _) => holders.Contains(own.Value, StringComparer.Ordinal) ? CitationOutcome.Resolved : CitationOutcome.OtherRepo,
            (null, 1) => CitationOutcome.Resolved,
            _ => CitationOutcome.Ambiguous,
        };
    }

    /// <summary>Splits a 'touches' link's 'path:&lt;repo&gt;/&lt;path&gt;' (a repo name may hold '/').</summary>
    /// <param name="toRef">The link's target.</param>
    /// <returns>The repo's name and the path, or null when no mapped repo owns the prefix.</returns>
    internal (string Repo, string Path)? Touched(string toRef)
    {
        foreach (var name in _longestFirst)
        {
            var prefix = $"path:{name}/";
            if (toRef.StartsWith(prefix, StringComparison.Ordinal))
            {
                return (name, toRef[prefix.Length..]);
            }
        }

        return null;
    }

    /// <summary>Whether a repo's path list holds a path.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="path">The path.</param>
    /// <returns>True when it does.</returns>
    internal bool Has(string repo, string path) => _byRepo.TryGetValue(repo, out var paths) && paths.Contains(path);
}
