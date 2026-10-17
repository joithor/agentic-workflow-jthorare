using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core;
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
        var settings = engine.CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))]);
        return settings.IsSuccess && ManagedVersion(settings.Value.Onyx) is { } stale ? stale : settings;
    }

    // A managed Onyx is the release this build pins (`sindri onyx up` runs nothing else): a profile that pins another is stale, and says what to do.
    private Error? ManagedVersion(Aw.Context.Core.Onyx.OnyxSettings? onyx)
        => onyx is { Managed: true } && !string.Equals(Bare(onyx.Version), Bare(pins.Pins.Version), StringComparison.Ordinal)
            ? ContextErrors.OnyxNotPinnedOrLoopback.With(
                $"context.sources.onyx.version: Sindri's managed Onyx runs {pins.Pins.Version}, but the profile pins '{onyx.Version}'. Set version to {pins.Pins.Version}, or run `sindri onyx up` to write the entry again.")
            : null;

    private static string Bare(string version) => version.StartsWith('v') ? version[1..] : version;
}
