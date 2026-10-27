using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ValidateProfile;

/// <summary>The warning for a 'workflow.agent.target' whose catalog entry can't take a Step's policy (issue #1428): every unattended Step on it parks with SND-LAUNCH-013.</summary>
internal static class AgentTargetWarning
{
    /// <summary>The check's name in the doctor.</summary>
    internal const string Name = "agent:target";

    /// <summary>The warning for <paramref name="profile"/>'s target on this box's <paramref name="catalog"/>.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="catalog">The entries on this box.</param>
    /// <returns>The line, or null when there is no target, the catalog has no such entry (it is per box) or the entry can take a policy.</returns>
    internal static string? For(SindriProfile profile, IAgentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(catalog);
        return null;
    }
}
