using System.Text;
using System.Text.Json;
using Aw.Kernel.Events;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Outbox;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// Builds ledger entries and outbox commands with the scope's actor, tenant and ring and the payload as AwJson: a fresh
/// correlation per command, a given correlation (all events of one workflow instance), or caused by an earlier event.
/// </summary>
/// <param name="scope">Who the process acts as.</param>
/// <param name="clock">The clock.</param>
/// <param name="ids">The id source.</param>
/// <param name="json">The package's AwJson options (the payload records are in its context).</param>
public sealed class EventFactory(EventScope scope, TimeProvider clock, IIdGenerator ids, JsonSerializerOptions json)
{
    /// <summary>An entry of <paramref name="type"/> with <paramref name="payload"/> and the idempotency <paramref name="key"/>, starting a new correlation.</summary>
    /// <param name="type">The event type, for example 'process.started'.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key: what identifies the fact, so recording it twice is a no-op.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>The entry.</returns>
    public LedgerEntry Entry<TPayload>(string type, TPayload payload, string key) =>
        Build(Context(ids.NewId()), type, payload, key, item: null);

    /// <summary>An entry about <paramref name="item"/> in the existing <paramref name="correlation"/> (for example every event of one workflow instance).</summary>
    /// <param name="correlation">The correlation id the entry joins.</param>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="item">The work item, if any.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>The entry.</returns>
    internal LedgerEntry Correlated<TPayload>(Guid correlation, string type, TPayload payload, string key, ItemId? item) =>
        Build(Context(correlation), type, payload, key, item);

    /// <summary>An entry caused by <paramref name="cause"/>: its correlation, with the causation set to the cause's id.</summary>
    /// <param name="cause">The causing event.</param>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="item">The work item, if any.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>The entry.</returns>
    public LedgerEntry Caused<TPayload>(EventEnvelope<string> cause, string type, TPayload payload, string key, ItemId? item)
    {
        ArgumentNullException.ThrowIfNull(cause);
        return Build(Context(cause.CorrelationId).CausedBy(cause), type, payload, key, item);
    }

    /// <summary>An outbox command caused by <paramref name="cause"/> (an effect of the transition it records), in the same correlation.</summary>
    /// <param name="cause">The event that emits it.</param>
    /// <param name="kind">The command kind.</param>
    /// <param name="payloadJson">The payload JSON.</param>
    /// <returns>The command.</returns>
    public OutboxCommand Command(EventEnvelope<string> cause, string kind, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(cause);
        return Context(cause.CorrelationId).CausedBy(cause).Command(kind, payloadJson, clock, ids);
    }

    private EventContext Context(Guid correlation) => EventContext.Create(scope.Actor, scope.Tenant, scope.Ring, correlation).Value;

    private LedgerEntry Build<TPayload>(EventContext context, string type, TPayload payload, string key, ItemId? item)
    {
        var text = Encoding.UTF8.GetString(AwJson.Serialize(payload, json));
        return new LedgerEntry(context.Envelope(type, text, clock, ids), key, item);
    }
}
