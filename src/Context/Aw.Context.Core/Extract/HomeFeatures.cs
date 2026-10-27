using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Extract;

/// <summary>Gives every product file of a repo exactly one home feature, chosen by directory and never by a model (spec 2026-10-27 §2).</summary>
public static class HomeFeatures
{
    /// <summary>The most files a home feature holds before its directory is split one level.</summary>
    public const int SizeCap = 150;

    /// <summary>Assigns the home features of a repo's files.</summary>
    /// <param name="repo">The repo: its name joins the keys, its deny, demote and role globs say which files get no home.</param>
    /// <param name="paths">The repo's full path list (before the readable-file filter, so a '.csproj' can mark a module root).</param>
    /// <param name="extracted">The deterministic extraction: a surface is 'part-of' the home feature of its defining file; null claims none.</param>
    /// <returns>The features, their links and the directory-to-feature lookup.</returns>
    public static HomeAssignment Assign(ContextRepo repo, IReadOnlyList<string> paths, ExtractedMap? extracted = null) =>
        throw new NotSupportedException("red: HomeFeatures.Assign");
}
