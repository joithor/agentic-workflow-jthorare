using Aw.Kernel;
using Aw.Sindri.Core.Blocks.Evidence;

namespace Aw.Sindri.Testing;

/// <summary>
/// An <see cref="IUiEvidence"/> whose stack, database and runs the test sets: a run 'writes' its document at
/// &lt;run-dir&gt;/summary.json (parity.json) with a digest derived from the run, and every call is recorded.
/// </summary>
public sealed class FakeUiEvidence : IUiEvidence
{
    private readonly List<string> _calls = [];

    /// <summary>What doctor says (healthy by default).</summary>
    public StackHealth Health { get; set; } = new(Healthy: true, []);

    /// <summary>What provenance says (seeded by default).</summary>
    public DatabaseProvenance DatabaseProvenance { get; set; } = new(DataProvenance.Seeded, "seed row present");

    /// <summary>Whether runs and parity measurements pass.</summary>
    public bool RunsPass { get; set; } = true;

    /// <summary>When set, every call fails with it (the tool couldn't be run).</summary>
    public Error? Failure { get; set; }

    /// <summary>Every call, as 'command' or 'command input run-dir host sha'.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <inheritdoc/>
    public ValueTask<Result<StackHealth>> Doctor(CancellationToken cancellationToken) => Answer("doctor", Health);

    /// <inheritdoc/>
    public ValueTask<Result<DatabaseProvenance>> Provenance(CancellationToken cancellationToken) => Answer("provenance", DatabaseProvenance);

    /// <inheritdoc/>
    public ValueTask<Result<EvidenceDocument>> Run(UiEvidenceRun run, CancellationToken cancellationToken) => Measure("run", "summary.json", run);

    /// <inheritdoc/>
    public ValueTask<Result<EvidenceDocument>> Parity(UiEvidenceRun run, CancellationToken cancellationToken) => Measure("parity", "parity.json", run);

    /// <summary>The digest the fake gives a run's document.</summary>
    /// <param name="run">The run.</param>
    /// <returns>'sha256:' and 64 hex digits.</returns>
    public static string DigestOf(UiEvidenceRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{run.Input}|{run.AppBuild.Value}")));
    }

    private ValueTask<Result<EvidenceDocument>> Measure(string command, string file, UiEvidenceRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var path = Path.Combine(Path.GetFullPath(run.RunDirectory), file);
        return Answer($"{command} {run.Input} {run.RunDirectory} {run.Host} {run.AppBuild.Value}", new EvidenceDocument(RunsPass, path, DigestOf(run)));
    }

    private ValueTask<Result<T>> Answer<T>(string call, T value)
    {
        _calls.Add(call);
        return ValueTask.FromResult(Failure is { } failure ? (Result<T>)failure : value);
    }
}
