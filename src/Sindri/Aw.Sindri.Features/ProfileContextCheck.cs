using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Onyx;
using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features;

/// <summary>IProfileContextCheck over the context engine and this build's Onyx pins.</summary>
/// <param name="engine">The context engine.</param>
/// <param name="pins">The Onyx release this build pins.</param>
internal sealed class ProfileContextCheck(IContextEngine engine, IOnyxPinsSource pins) : IProfileContextCheck
{
    /// <inheritdoc />
    public Error? Check(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var settings = engine.CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))]);
        return settings.IsError ? settings.Error : ManagedVersion(settings.Value.Onyx);
    }

    // A managed Onyx is the release this build pins (`sindri onyx up` runs nothing else): a profile that pins another is stale, and says what to do.
    private Error? ManagedVersion(OnyxSettings? onyx)
        => onyx is { Managed: true } && !string.Equals(Bare(onyx.Version), Bare(pins.Pins.Version), StringComparison.Ordinal)
            ? ContextErrors.OnyxNotPinnedOrLoopback.With(
                $"context.sources.onyx.version: Sindri's managed Onyx runs {pins.Pins.Version}, but the profile pins '{onyx.Version}'. Set version to {pins.Pins.Version}, or run `sindri onyx up` to write the entry again.")
            : null;

    private static string Bare(string version) => version.StartsWith('v') ? version[1..] : version;
}
