using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Ids;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IUiEvidence"/> honours (the ui-evidence CLI contract, docs/csharp/evidence.md), run against the fake and the process adapter.</summary>
public abstract class UiEvidenceContract
{
    /// <summary>A sha for app builds.</summary>
    protected static HeadSha Sha { get; } = HeadSha.From(new string('c', 40));

    /// <summary>A client over a tool that answers <paramref name="scenario"/>.</summary>
    /// <param name="scenario">What the tool says.</param>
    /// <returns>The client.</returns>
    protected abstract IUiEvidence Create(UiEvidenceScenario scenario);

    /// <summary>A run directory for this test.</summary>
    /// <returns>An absolute directory path.</returns>
    protected abstract string RunDirectory();

    /// <summary>A healthy stack reads healthy; an unhealthy one reads with its failures.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Doctor_reads_health_and_failures()
    {
        var ct = TestContext.Current.CancellationToken;
        var healthy = await Create(new UiEvidenceScenario(Health: new(Healthy: true, []))).Doctor(ct);
        var sick = await Create(new UiEvidenceScenario(Health: new(Healthy: false, ["the database is not ready"]))).Doctor(ct);

        Expect.True(healthy.Value.Healthy && healthy.Value.Failures.Count == 0);
        Expect.True(!sick.Value.Healthy);
        Expect.Sequence(["the database is not ready"], sick.Value.Failures);
    }

    /// <summary>Provenance reads seeded or unknown with its reason.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Provenance_reads_seeded_or_unknown_with_the_reason()
    {
        var ct = TestContext.Current.CancellationToken;
        var unknown = await Create(new UiEvidenceScenario(Provenance: new(DataProvenance.Unknown, "no seed row found"))).Provenance(ct);
        var seeded = await Create(new UiEvidenceScenario(Provenance: new(DataProvenance.Seeded, "seed row present"))).Provenance(ct);

        Expect.Equal(new DatabaseProvenance(DataProvenance.Unknown, "no seed row found"), unknown.Value);
        Expect.Equal(new DatabaseProvenance(DataProvenance.Seeded, "seed row present"), seeded.Value);
    }

    /// <summary>run and parity each name their document in the run directory with a sha256 digest, passed or not (a failed check is still a document).</summary>
    /// <param name="parity">Parity rather than run.</param>
    /// <param name="passes">Whether the checks pass.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task A_run_names_its_document_and_digest_passed_or_not(bool parity, bool passes)
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = RunDirectory();
        var run = new UiEvidenceRun("/work/script.json", dir, "http://localhost:3000", Sha);
        var client = Create(new UiEvidenceScenario(RunPasses: passes));

        var document = parity ? await client.Parity(run, ct) : await client.Run(run, ct);

        Expect.Equal(passes, document.Value.Passed);
        Expect.Equal(Path.Combine(dir, parity ? "parity.json" : "summary.json"), document.Value.Path);
        Expect.True(document.Value.Digest.StartsWith("sha256:", StringComparison.Ordinal) && document.Value.Digest.Length == 71);
    }

    /// <summary>A relative script or run directory is refused (SND-BLK-002) before the tool runs: the control plane's working directory isn't the caller's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Relative_paths_are_refused()
    {
        var refused = await Create(new UiEvidenceScenario()).Run(new UiEvidenceRun("script.json", "runs/r1", "http://localhost:3000", Sha), TestContext.Current.CancellationToken);

        Expect.True(refused.IsError && refused.Error.Code == SindriErrors.ProviderInputInvalid.Code);
    }

    /// <summary>A tool that errors (ui-evidence's exit 4 and its line) is SND-BLK-001 with the line.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_tool_error_is_tool_unavailable_with_its_line()
    {
        var client = Create(new UiEvidenceScenario(Error: "AW-EVIDENCE-011 the browser failed to start"));

        var run = await client.Run(new UiEvidenceRun("/work/script.json", RunDirectory(), "http://localhost:3000", Sha), TestContext.Current.CancellationToken);
        var doctor = await client.Doctor(TestContext.Current.CancellationToken);

        Expect.True(run.IsError && run.Error.Code == SindriErrors.ToolUnavailable.Code && run.Error.Message.Contains("AW-EVIDENCE-011", StringComparison.Ordinal));
        Expect.True(doctor.IsError && doctor.Error.Code == SindriErrors.ToolUnavailable.Code);
    }
}
