using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The item and repo of every launch, read from the ledger (sandbox spec §3.6) and never from views.db, whose projector may lag: 'launch.started' names the item, 'item.created' the item's repo.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class LedgerLaunchOwners(ILedgerReader ledger, EventFactory events) : ILaunchOwners
{
    public ValueTask<IReadOnlyDictionary<LaunchId, LaunchOwner>> Read(CancellationToken cancellationToken)
    {
        _ = (ledger, events);
        throw new NotSupportedException();
    }
}
