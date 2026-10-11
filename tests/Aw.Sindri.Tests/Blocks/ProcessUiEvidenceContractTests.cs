using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Infrastructure.Tools;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>The process adapter against a scripted ui-evidence that speaks its CLI contract (documents on stdout or in the run directory, exit 0 / 2 / 4).</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessUiEvidenceContractTests : UiEvidenceContract, IDisposable
{
    private static readonly string _script = string.Join(
        '\n',
        "printf '%s\\n' \"$@\" > \"$d/argv\"",
        "if [ -f \"$d/error\" ]; then cat \"$d/error\" >&2; exit 4; fi",
        "case \"$1\" in",
        "  doctor) cat \"$d/doctor.json\"; exit $(cat \"$d/doctor.exit\") ;;",
        "  provenance) cat \"$d/provenance.json\"; exit 0 ;;",
        "  run) mkdir -p \"$3\"; printf '{\"script\":\"%s\",\"build\":\"%s\"}' \"$2\" \"$7\" > \"$3/summary.json\"; exit $(cat \"$d/run.exit\") ;;",
        "  parity) mkdir -p \"$3\"; printf '{\"manifest\":\"%s\"}' \"$2\" > \"$3/parity.json\"; exit $(cat \"$d/run.exit\") ;;",
        "  *) echo \"ui-evidence: usage: ui-evidence <command>\" >&2; exit 1 ;;",
        "esac",
        string.Empty);

    private readonly StubTool _tool = new(_script);

    public void Dispose() => _tool.Dispose();

    [Fact]
    public async Task A_run_passes_the_arguments_and_digests_the_written_document()
    {
        var dir = RunDirectory();
        var client = Create(new UiEvidenceScenario());

        var document = (await client.Run(new UiEvidenceRun("/work/script.json", dir, "http://localhost:4000", Sha), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["run", "/work/script.json", dir, "--host", "http://localhost:4000", "--app-build", Sha.Value], _tool.Read("argv").TrimEnd('\n').Split('\n'));
        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(document.Path, TestContext.Current.CancellationToken))), document.Digest);
    }

    [Fact]
    public async Task Parity_passes_its_manifest_run_directory_host_and_build()
    {
        var dir = RunDirectory();

        _ = (await Create(new UiEvidenceScenario()).Parity(new UiEvidenceRun("/work/manifest.json", dir, "http://localhost:4000", Sha), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["parity", "/work/manifest.json", dir, "--host", "http://localhost:4000", "--app-build", Sha.Value], _tool.Read("argv").TrimEnd('\n').Split('\n'));
    }

    [Fact]
    public async Task A_run_that_writes_no_document_is_tool_unavailable()
    {
        await File.WriteAllTextAsync(_tool.Binary, "#!/bin/sh\nexit 0\n", TestContext.Current.CancellationToken);

        var document = await Create(new UiEvidenceScenario()).Run(new UiEvidenceRun("/s.json", RunDirectory(), "http://localhost:3000", Sha), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ToolUnavailable.Code, document.Error.Code);
    }

    protected override IUiEvidence Create(UiEvidenceScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var health = scenario.Health ?? new StackHealth(Healthy: true, []);
        _tool.Write("doctor.json", Encoding.UTF8.GetString(AwJson.Serialize(health, SliceHarness.Json)));
        _tool.Write("doctor.exit", health.Healthy ? "0" : "2");
        _tool.Write("provenance.json", Encoding.UTF8.GetString(AwJson.Serialize(scenario.Provenance ?? new DatabaseProvenance(DataProvenance.Seeded, "seed"), SliceHarness.Json)));
        _tool.Write("run.exit", scenario.RunPasses ? "0" : "2");
        if (scenario.Error is { } line)
        {
            _tool.Write("error", $"ui-evidence: {line}\n");
        }

        return new ProcessUiEvidence(new ToolSettings("judge", _tool.Binary), SliceHarness.Json);
    }

    protected override string RunDirectory() => Path.Combine(_tool.Directory, "run");
}
