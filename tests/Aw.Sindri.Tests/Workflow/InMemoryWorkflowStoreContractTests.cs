using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryWorkflowStoreContractTests : WorkflowStoreContract
{
    protected override ValueTask<WorkflowStores> Create(TimeProvider clock, IProcessProbe probe)
    {
        var lease = new InMemorySindriStore(clock, probe);
        var store = new InMemoryWorkflowStore(lease);
        return ValueTask.FromResult(new WorkflowStores(lease, store, () => ValueTask.FromResult(store.Commands)));
    }
}
