using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Pr;

/// <summary>A ledger reader holding 'pr.pushed' reply rows (and the 'fact:push' content they refer to), as the effect's reply event writes them.</summary>
internal sealed class PushedLedger(InMemoryFactStore facts) : ILedgerReader
{
    private static readonly string[] _one = ["c2"];
    private readonly List<LedgerEvent> _events = [];

    /// <summary>Adds a reply of <paramref name="outcome"/> that pushed for <paramref name="pr"/> with <paramref name="cause"/>, for any instance.</summary>
    internal void Push(string pr, string cause, string outcome = "pushed", string type = "pr.pushed")
    {
        var content = JsonSerializer.Serialize(new { sha = "c2", commits = _one, comments = Array.Empty<string>(), source = true, pr, cause });
        var stored = facts.Put(Encoding.UTF8.GetBytes(content), CancellationToken.None).AsTask().GetAwaiter().GetResult().Value;
        var payload = JsonSerializer.Serialize(new { outcome, effect = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"WFI-{_events.Count}:2:0"), facts = new Dictionary<string, object>(StringComparer.Ordinal) { ["fact:push"] = new { @ref = stored.Ref, sha256 = stored.Sha256, headSha = (string?)null } } });
        var sequence = _events.Count + 1;
        _events.Add(new LedgerEvent(sequence, 1, new EventEnvelope<string>(Id(sequence), type, SchemaVersion: 1, DateTimeOffset.UnixEpoch, "system:dispatcher", "t", Ring: 1, Id(0), CausationId: null, TraceId: null, payload), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"k{sequence}"), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"T-{sequence}")));
    }

    private static Guid Id(int n) => new(n, 0, 0, new byte[8]);

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<LedgerEvent>>>(Results.Success<IReadOnlyList<LedgerEvent>>([.. _events.Where(e => e.Sequence > sequence).Take(max)]));

    public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken)
    {
        var kept = _events.Where(e => e.Sequence > sequence && filter.Keeps(e)).Take(max).ToList();
        return ValueTask.FromResult<Result<LedgerScan>>(filter.Scanned(sequence, _events.Count, max, kept));
    }

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LedgerEvent?>>(_events.LastOrDefault(e => e.Envelope.Type == type));

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => ValueTask.FromResult<Result<long>>(_events.Count);
}
