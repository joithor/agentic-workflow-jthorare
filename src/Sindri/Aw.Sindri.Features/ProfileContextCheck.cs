using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features;

/// <summary>
/// The one check of a profile's 'context:' section that 'profile validate' and approve share: the engine's (the keys, the patterns, the repos), then what only
/// Sindri knows. An invalid section is never approved (SND-CONTEXT-001, 002, 055).
/// </summary>
/// <param name="engine">The context engine.</param>
/// <param name="pins">The Onyx release this build pins.</param>
internal sealed class ProfileContextCheck(IContextEngine engine, IOnyxPinsSource pins)
{
    /// <summary>Checks the profile's context section against its repos.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The settings, or the first error.</returns>
    public Result<ContextSettings> Check(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _ = pins;
        return engine.CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))]);
    }
}
