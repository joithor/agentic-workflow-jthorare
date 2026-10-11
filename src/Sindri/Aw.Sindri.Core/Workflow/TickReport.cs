namespace Aw.Sindri.Core.Workflow;

/// <summary>What one timer tick did.</summary>
/// <param name="Fired">Timers that fired (parked, escalated or retried an instance).</param>
/// <param name="Extended">Timers moved after a clock jump.</param>
public sealed record TickReport(int Fired, int Extended);
