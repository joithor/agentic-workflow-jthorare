namespace Aw.Sindri.Features.ShowWorkflow;

/// <summary>One event of an instance's history.</summary>
/// <param name="Sequence">The ledger position.</param>
/// <param name="Type">The event type, for example 'workflow.transitioned'.</param>
/// <param name="At">When.</param>
/// <param name="Epoch">The lease epoch it was written under.</param>
/// <param name="Key">The idempotency key.</param>
public sealed record HistoryEntry(long Sequence, string Type, DateTimeOffset At, long Epoch, string Key);
