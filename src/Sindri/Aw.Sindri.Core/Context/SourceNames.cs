namespace Aw.Sindri.Core.Context;

/// <summary>The names a person types for each <see cref="SourceKind"/> (--sources, the CLI's text).</summary>
public static class SourceNames
{
    private static readonly (SourceKind Kind, string Name)[] _names =
    [
        (SourceKind.Code, "code"), (SourceKind.Notes, "notes"), (SourceKind.Linear, "linear"), (SourceKind.ConnectAi, "connectai"),
    ];

    /// <summary>Every name, in source order, for messages.</summary>
    public static string All { get; } = string.Join(", ", _names.Select(n => n.Name));

    /// <summary>The name of <paramref name="kind"/>.</summary>
    /// <param name="kind">The source.</param>
    /// <returns>Its name.</returns>
    public static string Of(SourceKind kind) => _names.First(n => n.Kind == kind).Name;

    /// <summary>Reads a source name, ignoring case.</summary>
    /// <param name="name">The name (null or blank is no source).</param>
    /// <param name="kind">The source, when the name is known.</param>
    /// <returns>True when the name is a source.</returns>
    public static bool TryParse(string? name, out SourceKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var (k, n) in _names)
        {
            if (string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                kind = k;
                return true;
            }
        }

        return false;
    }
}
