namespace Aw.Sindri.Features.TickWorkflowTimers;

/// <summary>Fire due workflow timers.</summary>
/// <param name="ClockJump">How far the clock jumped since the last tick (after a sleep), or null.</param>
public sealed record TickWorkflowTimersRequest(TimeSpan? ClockJump = null);
