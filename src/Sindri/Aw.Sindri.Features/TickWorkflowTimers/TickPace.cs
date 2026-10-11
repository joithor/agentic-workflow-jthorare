namespace Aw.Sindri.Features.TickWorkflowTimers;

/// <summary>
/// The timer service's memory between runs: the last slot and the shortest gap seen. A gap much longer than that
/// (after a sleep) is a clock jump, and the timers move by it before they are evaluated (design spec §9.2).
/// </summary>
internal sealed class TickPace
{
    private static readonly TimeSpan _minimumJump = TimeSpan.FromSeconds(30);
    private readonly Lock _gate = new();
    private DateTimeOffset? _last;
    private TimeSpan? _shortest;

    /// <summary>Records <paramref name="slot"/> and returns the jump since the last slot, if the clock jumped.</summary>
    /// <param name="slot">This run's slot.</param>
    /// <returns>The jump, or null.</returns>
    internal TimeSpan? Observe(DateTimeOffset slot)
    {
        lock (_gate)
        {
            var gap = _last is { } last ? slot - last : (TimeSpan?)null;
            _last = slot;
            if (gap is not { } g || g <= TimeSpan.Zero)
            {
                return null;
            }

            var shortest = _shortest is { } s && s < g ? s : g;
            _shortest = shortest;
            var jump = g - shortest;
            return g > shortest * 3 && jump > _minimumJump ? jump : null;
        }
    }
}
