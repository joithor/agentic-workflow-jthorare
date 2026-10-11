using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing.Context;

/// <summary>Opens the fake sources a test registers, for the sources the profile names; passes <see cref="SourceCatalogContract"/>.</summary>
public sealed class FakeSourceCatalog : ISourceCatalog
{
    private readonly Dictionary<SourceKind, IContextSource> _sources = [];

    /// <summary>Registers the source a named kind opens to; a named kind with none registered is skipped.</summary>
    /// <param name="source">The source.</param>
    /// <returns>This catalog.</returns>
    public FakeSourceCatalog With(IContextSource source)
    {
        _sources[source.Kind] = source;
        return this;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SourceOpening> Open(SindriProfile profile) =>
        [.. NamedSources.In(profile).Select(kind => _sources.TryGetValue(kind, out var source)
            ? SourceOpening.Opened(source)
            : SourceOpening.Skip(kind, "no fake registered for it"))];
}
