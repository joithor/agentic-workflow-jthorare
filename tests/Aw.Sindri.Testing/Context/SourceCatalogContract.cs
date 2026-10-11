using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Xunit;

namespace Aw.Sindri.Testing.Context;

/// <summary>The contract every <see cref="ISourceCatalog"/> honours: one opening per named source, in source order; a skip carries SND-CONTEXT-002.</summary>
public abstract class SourceCatalogContract
{
    /// <summary>The catalog under test.</summary>
    /// <returns>The catalog.</returns>
    protected abstract ISourceCatalog Create();

    /// <summary>A checked profile naming all four sources, at least one of which this catalog can open.</summary>
    /// <returns>The profile.</returns>
    protected abstract SindriProfile ProfileNamingEverySource();

    /// <summary>A profile with no context section opens nothing.</summary>
    [Fact]
    public void A_profile_without_context_opens_nothing()
    {
        var openings = Create().Open(new SindriProfile(1, "thor"));

        Expect.Equal(0, openings.Count);
    }

    /// <summary>Each named source gets one opening, in source order: opened as its own kind, or skipped with SND-CONTEXT-002.</summary>
    [Fact]
    public void Each_named_source_is_opened_or_skipped_in_source_order()
    {
        var profile = ProfileNamingEverySource();

        var openings = Create().Open(profile);

        Expect.Sequence(NamedSources.In(profile), openings.Select(o => o.Kind));
        Expect.True(openings.All(Consistent));
        Expect.True(openings.Any(o => o.Source is not null));
    }

    private static bool Consistent(SourceOpening opening) => opening.Source is { } source
        ? source.Kind == opening.Kind && opening.Skipped is null
        : opening.Skipped is { } reason && reason.StartsWith(SindriErrors.ContextSourceUnusable.Code, StringComparison.Ordinal);
}
