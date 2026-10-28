
namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// Looks at the paths a repo tracks and suggests its 'sandbox.repos.&lt;repo&gt;' entries (sandbox spec §3.7): the host group or hosts, the cache paths and, for npm lock files, the prepare dirs of each toolchain it finds.
/// Pure: it takes a file listing, reads no disk, and writes nothing; the owner approves what it suggests with the profile.
/// </summary>
public static class SandboxToolchainDetector
{
    /// <summary>Detects the toolchains of a repo from its tracked paths.</summary>
    /// <param name="paths">The repo's tracked paths, relative to the repo and '/' separated.</param>
    /// <returns>Each toolchain found and the entries all of them add up to; no settings when none is found.</returns>
    public static ToolchainOffer Detect(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        throw new NotSupportedException();
    }
}
