using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The limit that killed an agent Step's session (sandbox spec §6, -019).</summary>
/// <param name="Kind">'memory' or 'wall' (a word, not Events.LimitKind, so Runtime stays below Events).</param>
/// <param name="Limit">The limit: bytes for memory, seconds for wall.</param>
/// <param name="Peak">The most the session used, in the same unit.</param>
public sealed record AgentSessionLimit(string Kind, long Limit, long Peak)
{
    /// <summary>The kind of a memory limit.</summary>
    public const string Memory = "memory";

    /// <summary>The kind of a wall-clock limit.</summary>
    public const string Wall = "wall";

    /// <summary>The words a failed Step carries after its reason 'limit': 'memory limit 2147483648 bytes, peak 2300000000 bytes' (wall is in seconds).</summary>
    public string Detail => string.Create(CultureInfo.InvariantCulture, $"{Kind} limit {Limit} {Unit}, peak {Peak} {Unit}");

    private string Unit => string.Equals(Kind, Memory, StringComparison.Ordinal) ? "bytes" : "seconds";
}
