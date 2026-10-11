using Aw.Kernel.Outbox;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Everything one transition commits in one transaction (§17.28.1): its event and new instance row, the events and rows
/// of instances it starts or propagates to (a child, a parent), and the outbox commands of its effects. The first
/// write's event key decides idempotency: if it is recorded already, nothing is written.
/// </summary>
/// <param name="Writes">The instance writes, the triggering one first.</param>
/// <param name="Commands">The outbox commands.</param>
public sealed record WorkflowCommit(IReadOnlyList<InstanceWrite> Writes, IReadOnlyList<OutboxCommand> Commands);
