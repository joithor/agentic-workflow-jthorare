using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Context;

/// <summary>Which sources a profile turns on: those whose key is present under context:, in source order.</summary>
public static class NamedSources
{
    /// <summary>The sources <paramref name="profile"/> names.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The sources, in source order; none without a context section.</returns>
    public static IReadOnlyList<SourceKind> In(SindriProfile profile)
    {
        if (profile.Context is not { } context)
        {
            return [];
        }

        var named = new List<SourceKind>();
        if (context.Code is not null)
        {
            named.Add(SourceKind.Code);
        }

        if (context.Notes is not null)
        {
            named.Add(SourceKind.Notes);
        }

        if (context.Linear is not null)
        {
            named.Add(SourceKind.Linear);
        }

        if (context.ConnectAi is not null)
        {
            named.Add(SourceKind.ConnectAi);
        }

        return named;
    }
}
