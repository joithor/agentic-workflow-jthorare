using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Host.Bodies;

namespace Aw.Sindri.Host.Commands;

/// <summary>The 'sindri workflow' verbs that change an instance: signal, evidence, collect, cancel, park, unpark, takeover, handback.</summary>
internal sealed class WorkflowCommands(WorkflowVerbs verbs, WorkflowArgs args)
{
    private static readonly Dictionary<string, ParkReason> _reasons = new(StringComparer.Ordinal)
    {
        ["rounds"] = ParkReason.Rounds,
        ["budget"] = ParkReason.Budget,
        ["no-progress"] = ParkReason.NoProgress,
        ["timeout"] = ParkReason.Timeout,
        ["needs-answer"] = ParkReason.NeedsAnswer,
        ["spec-conflict"] = ParkReason.SpecConflict,
    };

    // The evidence providers' path inputs, made absolute against this shell's directory before they're sent.
    private static readonly HashSet<string> _pathInputs = new(StringComparer.Ordinal) { "script", "manifest", "run-dir" };

    internal Task<int> Run(CancellationToken ct)
    {
        var p = args.Positionals;
        return (p[0], p.Count) switch
        {
            ("signal", 3) => verbs.WithId(p[1], id => Signal(id.Value, p[2], ct)),
            ("evidence", 5) => verbs.WithId(p[1], id => Evidence(id.Value, ct)),
            ("collect", 4) => verbs.WithId(p[1], id => Collect(id.Value, ct)),
            ("cancel", 2) => verbs.WithId(p[1], id => verbs.Mutate($"/sindri/workflows/{id.Value}/cancel", new ReasonBody(args.Option("--reason")), args, ct)),
            ("park", 3) => verbs.WithId(p[1], id => Park(id.Value, p[2], ct)),
            ("unpark", 2) => verbs.WithId(p[1], id => Unpark(id.Value, ct)),
            ("takeover", 2) => verbs.WithId(p[1], id => verbs.Mutate<ReasonBody>($"/sindri/workflows/{id.Value}/takeover", body: null, args, ct)),
            ("handback", 2) => verbs.WithId(p[1], id => verbs.Mutate<ReasonBody>($"/sindri/workflows/{id.Value}/handback", body: null, args, ct)),
            _ => verbs.Usage($"'{string.Join(' ', p)}' is not a workflow verb"),
        };
    }

    private Task<int> Signal(string id, string on, CancellationToken ct)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in args.All("--set"))
        {
            var at = pair.IndexOf('=', StringComparison.Ordinal);
            if (at < 1)
            {
                return verbs.Usage($"--set takes name=value, not '{pair}'");
            }

            outputs[pair[..at]] = pair[(at + 1)..];
        }

        if (args.BadNumber("--expect-version") || args.BadNumber("--tokens") || args.BadNumber("--turns") || Sha() is { Bad: true })
        {
            return verbs.Usage("--expect-version, --tokens and --turns take whole numbers; --sha takes 7 to 64 lower-case hex digits");
        }

        var body = new SignalBody(on, args.Number("--expect-version"), Sha()?.Sha, outputs, [.. args.All("--finding")], args.Flag("--empty-diff"),
            args.Number("--tokens") ?? 0, (int)Math.Min(args.Number("--turns") ?? 0, int.MaxValue));
        return verbs.Mutate($"/sindri/workflows/{id}/signal", body, args, ct);
    }

    private Task<int> Evidence(string id, CancellationToken ct)
    {
        var p = args.Positionals;
        return Core.Ids.HeadSha.TryFrom(p[3], out var sha)
            ? verbs.Mutate($"/sindri/workflows/{id}/evidence", new EvidenceBody(p[2], sha, p[4]), args, ct)
            : verbs.Usage($"evidence <id> <kind> <sha> <digest>: '{p[3]}' is not a commit sha");
    }

    private Task<int> Collect(string id, CancellationToken ct)
    {
        var p = args.Positionals;
        if (!Core.Ids.HeadSha.TryFrom(p[3], out var sha))
        {
            return verbs.Usage($"collect <id> <kind> <sha>: '{p[3]}' is not a commit sha");
        }

        var inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in args.All("--input"))
        {
            var at = pair.IndexOf('=', StringComparison.Ordinal);
            if (at < 1)
            {
                return verbs.Usage($"--input takes name=value, not '{pair}'");
            }

            var (name, value) = (pair[..at], pair[(at + 1)..]);
            inputs[name] = _pathInputs.Contains(name) && value.Trim().Length > 0 ? Path.GetFullPath(value) : value;   // the control plane's cwd isn't ours
        }

        return verbs.Collect($"/sindri/workflows/{id}/evidence/collect", new CollectBody(p[2], sha, inputs), args, ct);
    }

    private Task<int> Park(string id, string reason, CancellationToken ct) =>
        _reasons.TryGetValue(reason, out var parsed)
            ? verbs.Mutate($"/sindri/workflows/{id}/park", new ParkBody(parsed, args.Option("--detail")), args, ct)
            : verbs.Usage($"park <id> <reason>: the reason is one of {string.Join(", ", _reasons.Keys)}");

    private Task<int> Unpark(string id, CancellationToken ct) =>
        args.BadNumber("--tokens") || args.BadNumber("--turns")
            ? verbs.Usage("--tokens and --turns take whole numbers")
            : verbs.Mutate($"/sindri/workflows/{id}/unpark", new BudgetBody(args.Number("--tokens") ?? 0, (int)Math.Min(args.Number("--turns") ?? 0, int.MaxValue)), args, ct);

    private (Core.Ids.HeadSha? Sha, bool Bad)? Sha() => args.Option("--sha") is { } text
        ? Core.Ids.HeadSha.TryFrom(text, out var sha) ? (sha, false) : (null, true)
        : null;
}
