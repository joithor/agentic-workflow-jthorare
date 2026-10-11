namespace Aw.Sindri.Core.Context;

/// <summary>Fixed limits of a context query.</summary>
public static class ContextQueryDefaults
{
    /// <summary>How long each source has to answer; past it, the source counts as failed (SND-CONTEXT-010) and the query goes on.</summary>
    public static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(4);
}
