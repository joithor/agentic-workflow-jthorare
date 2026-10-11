using Aw.Kernel.Outbox;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Testing;

/// <summary>A workflow store with the lease it is fenced by, and a way to read the outbox commands it committed.</summary>
/// <param name="Lease">The lease.</param>
/// <param name="Store">The workflow store.</param>
/// <param name="Commands">Reads the committed outbox commands.</param>
public sealed record WorkflowStores(ILease Lease, IWorkflowStore Store, Func<ValueTask<IReadOnlyList<OutboxCommand>>> Commands);
