using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests;

/// <summary>The profile services' context check over the fake engine and this build's real Onyx pins.</summary>
internal static class ProfileContextChecks
{
    /// <summary>Gets a check over <see cref="FakeContextEngine.Instance"/>.</summary>
    internal static IProfileContextCheck Default => new ProfileContextCheck(FakeContextEngine.Instance, new FakeOnyxPins(new EmbeddedOnyxPins().Pins));
}
