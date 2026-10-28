using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Writes an offered 'sandbox.repos.&lt;repo&gt;' entry as the YAML to paste into the profile.</summary>
public static class SandboxOfferYaml
{
    /// <summary>Renders the snippet.</summary>
    /// <param name="repo">The repo's name in the profile.</param>
    /// <param name="settings">The offered entries.</param>
    /// <returns>The lines of a 'sandbox:' section holding only this repo's entry, each ending in a line break.</returns>
    public static string Render(string repo, SandboxRepoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(settings);
        throw new NotSupportedException();
    }
}
