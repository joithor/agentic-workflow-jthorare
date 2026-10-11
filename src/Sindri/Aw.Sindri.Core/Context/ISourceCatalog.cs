using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Context;

/// <summary>Opens the context sources an approved profile names (port): one opening per named source, in source order.</summary>
public interface ISourceCatalog
{
    /// <summary>Opens every source <paramref name="profile"/>'s context section names.</summary>
    /// <param name="profile">The approved profile (its repos and its context section).</param>
    /// <returns>One opening per named source; none when the profile has no context section.</returns>
    public IReadOnlyList<SourceOpening> Open(SindriProfile profile);
}
