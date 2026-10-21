namespace Aw.Sindri.Features.Pr;

/// <summary>What a path in a pull request is, by convention (PR babysitter spec §10.1 'gate.pr.diff-scope'): a test, a CI config, or source.</summary>
internal static class PrPaths
{
    /// <summary>Whether <paramref name="path"/> is a test by convention.</summary>
    /// <param name="path">A repo-relative path.</param>
    /// <returns>True for a test path.</returns>
    public static bool IsTest(string path) => throw new NotSupportedException();

    /// <summary>Whether <paramref name="path"/> is CI configuration the fix must not touch.</summary>
    /// <param name="path">A repo-relative path.</param>
    /// <returns>True for a CI config path.</returns>
    public static bool IsCiConfig(string path) => throw new NotSupportedException();

    /// <summary>Whether <paramref name="path"/> is source: not a test, not CI configuration, not a document.</summary>
    /// <param name="path">A repo-relative path.</param>
    /// <returns>True for a source path.</returns>
    public static bool IsSource(string path) => throw new NotSupportedException();
}
