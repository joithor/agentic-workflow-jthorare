namespace Aw.Sindri.Core.Workflow;

/// <summary>What a workflow command did: the instance after it, whether it was a retry of something already recorded, and the gates evaluated.</summary>
/// <param name="Instance">The instance now.</param>
/// <param name="AlreadyRecorded">True when nothing new was written (a retried or replayed command).</param>
/// <param name="Gates">The gates evaluated for a signal.</param>
public sealed record WorkflowOutcome(WorkflowInstance Instance, bool AlreadyRecorded, IReadOnlyList<GateRecord> Gates);
