using Aw.Context.Core.Records;

namespace Aw.Context.Core.Audience;

/// <summary>The audiences one caller may read (spec §4.4, §10): the set a mask over the store is built from.</summary>
/// <param name="Allowed">The audience keys the caller may see.</param>
public sealed record AudienceScope(IReadOnlySet<AudienceKey> Allowed)
{
    /// <summary>A scope that allows nothing.</summary>
    public static AudienceScope None { get; } = new(new HashSet<AudienceKey>());

    /// <summary>Whether a record with this audience is visible: a record with none is visible to no one (default deny).</summary>
    /// <param name="audience">The record's audience.</param>
    /// <returns>True when allowed.</returns>
    public bool Allows(AudienceKey? audience) => audience is { } key && Allowed.Contains(key);
}
