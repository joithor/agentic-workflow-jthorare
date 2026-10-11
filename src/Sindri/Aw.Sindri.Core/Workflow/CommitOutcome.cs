namespace Aw.Sindri.Core.Workflow;

/// <summary>What a commit did.</summary>
/// <param name="AlreadyRecorded">True when the first write's key was recorded already: a retry or replay, and nothing was written.</param>
/// <param name="Sequences">The ledger positions of the events written, in write order (the earlier position on a replay).</param>
public sealed record CommitOutcome(bool AlreadyRecorded, IReadOnlyList<long> Sequences);
