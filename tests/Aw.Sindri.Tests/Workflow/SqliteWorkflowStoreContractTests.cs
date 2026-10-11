using Aw.Kernel.Data;
using Aw.Kernel.Outbox;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Integration)]
public sealed class SqliteWorkflowStoreContractTests : WorkflowStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ValueTask<WorkflowStores> Create(TimeProvider clock, IProcessProbe probe)
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        var store = new SqliteWorkflowStore(database, SliceHarness.Json, clock);
        return ValueTask.FromResult(new WorkflowStores(new SqliteLease(database, probe, clock), store, () => Pending(database, clock)));
    }

    private static async ValueTask<IReadOnlyList<OutboxCommand>> Pending(LedgerDatabase database, TimeProvider clock)
    {
        var read = (await database.Sessions.BeginRead(TestContext.Current.CancellationToken)).Value;
        await using (read)
        {
            return (await new SqliteOutbox(read, clock, OutboxRetryPolicy.Default).ReadPending(100, TestContext.Current.CancellationToken)).Value;
        }
    }
}
