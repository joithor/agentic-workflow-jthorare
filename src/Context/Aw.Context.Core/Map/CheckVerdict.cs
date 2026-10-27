namespace Aw.Context.Core.Map;

/// <summary>How one structure measure came out.</summary>
public enum CheckVerdict
{
    /// <summary>At or under the degraded limit, or not measured.</summary>
    Pass,

    /// <summary>Over the degraded limit: the draft is saved with a note.</summary>
    Degraded,

    /// <summary>Over the refuse limit: no draft is saved.</summary>
    Refuse,
}
