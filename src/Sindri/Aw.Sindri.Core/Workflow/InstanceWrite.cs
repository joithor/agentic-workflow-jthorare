using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Workflow;

/// <summary>One instance's part of a commit: the event that records the change, the instance after it, and the version it must still be at.</summary>
/// <param name="Event">The ledger event (its payload is the <see cref="WorkflowChange"/>).</param>
/// <param name="After">The instance after the change.</param>
/// <param name="ExpectedVersion">The stored version it must have now: 0 for a new instance.</param>
public sealed record InstanceWrite(LedgerEntry Event, WorkflowInstance After, long ExpectedVersion);
