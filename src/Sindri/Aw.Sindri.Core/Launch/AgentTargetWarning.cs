using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Launch;

/// <summary>The warning for a 'workflow.agent.target' whose catalog entry can't take a Step's policy (issue #1428): every unattended Step on it parks with SND-LAUNCH-013.</summary>
public static class AgentTargetWarning
{
    /// <summary>The check's name in the doctor.</summary>
    public const string Name = "agent:target";

    /// <summary>The warning for <paramref name="profile"/>'s target on this box's <paramref name="catalog"/>.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="catalog">The entries on this box.</param>
    /// <returns>The line, or null when there is no target, the catalog has no such entry (it is per box) or the entry can take a policy.</returns>
    public static string? For(SindriProfile profile, IAgentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(catalog);
        if (profile.Workflow?.Agent?.Target is not { } target || catalog.Resolve(target) is not { } entry)
        {
            return null;
        }

        return entry.PolicyForm is PolicyForm.None or PolicyForm.CursorConfig
            ? $"workflow.agent.target '{target.Value}' can't run agent Steps: its policy form is {(entry.PolicyForm == PolicyForm.None ? "none" : "reserved")}, so every unattended Step parks with SND-LAUNCH-013 (fix: choose claude-code or codex, or give the entry a supported policy form)"
            : null;
    }
}
