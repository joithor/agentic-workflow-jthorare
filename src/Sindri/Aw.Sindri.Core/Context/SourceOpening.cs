namespace Aw.Sindri.Core.Context;

/// <summary>A source the profile names, opened, or the reason it can't be (SND-CONTEXT-002: no key, no directory, not wired).</summary>
/// <param name="Kind">The source.</param>
/// <param name="Source">The opened source, or null when skipped.</param>
/// <param name="Skipped">Why it was skipped, or null when opened.</param>
public sealed record SourceOpening(SourceKind Kind, IContextSource? Source, string? Skipped)
{
    /// <summary>An opened source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The opening.</returns>
    public static SourceOpening Opened(IContextSource source) => new(source.Kind, source, Skipped: null);

    /// <summary>A source that can't open, with SND-CONTEXT-002 and the reason.</summary>
    /// <param name="kind">The source.</param>
    /// <param name="reason">Why, with the fix.</param>
    /// <returns>The opening.</returns>
    public static SourceOpening Skip(SourceKind kind, string reason) =>
        new(kind, Source: null, $"{SindriErrors.ContextSourceUnusable.Code} {reason}");
}
