using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>The ui-browser and design-parity evidence providers: typed inputs in, a digest of the run's document out.</summary>
[Lane(TestLane.Unit)]
public sealed class UiRunProviderTests
{
    private static readonly HeadSha _sha = HeadSha.From(new string('d', 40));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_passing_run_yields_its_document_and_digest()
    {
        var ui = new FakeUiEvidence();

        var produced = (await UiRunProvider.UiBrowser(ui).Provide(Request(("script", "/s.json"), ("run-dir", "/runs/r1"), ("host", "http://localhost:3000")), Token)).Value;

        Assert.True(produced.Passed);
        Assert.Equal("/runs/r1/summary.json", produced.Document);
        Assert.Equal(FakeUiEvidence.DigestOf(new UiEvidenceRun("/s.json", "/runs/r1", "http://localhost:3000", _sha)), produced.Digest);
        Assert.Equal([$"run /s.json /runs/r1 http://localhost:3000 {_sha.Value}"], ui.Calls);
    }

    [Fact]
    public async Task Design_parity_measures_with_its_manifest_and_a_failed_check_doesnt_pass()
    {
        var ui = new FakeUiEvidence { RunsPass = false };

        var produced = (await UiRunProvider.DesignParity(ui).Provide(Request(("manifest", "/m.json"), ("run-dir", "/runs/p"), ("host", "http://localhost:3000")), Token)).Value;

        Assert.False(produced.Passed);
        Assert.Equal("/runs/p/parity.json", produced.Document);
        Assert.StartsWith("parity /m.json", ui.Calls.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_blank_or_unknown_inputs_are_refused_before_the_tool_runs()
    {
        var ui = new FakeUiEvidence();

        var error = (await UiRunProvider.UiBrowser(ui).Provide(Request(("script", " "), ("host", "http://localhost:3000"), ("viewport", "phone")), Token)).Error;

        Assert.Equal(SindriErrors.ProviderInputInvalid.Code, error.Code);
        Assert.Equal("evidence.ui-browser needs script:text; needs run-dir:text; doesn't take 'viewport' (inputs: script:text, run-dir:text, host:text)", error.Message);
        Assert.Empty(ui.Calls);
    }

    [Fact]
    public void Both_providers_are_evidence_blocks_with_typed_ports()
    {
        var specs = new[] { UiRunProvider.UiBrowser(new FakeUiEvidence()).Spec, UiRunProvider.DesignParity(new FakeUiEvidence()).Spec };

        Assert.Equal(["evidence.ui-browser", "evidence.design-parity"], specs.Select(s => s.Id.Value), StringComparer.Ordinal);
        Assert.All(specs, s => Assert.True(s is { Kind: BlockKind.Evidence, Capability: false, Rung: null }));
        Assert.Equal("script:text, run-dir:text, host:text", string.Join(", ", specs[0].Inputs));
        Assert.All(specs, s => Assert.Empty(s.Outputs));
    }

    private static EvidenceRequest Request(params (string Name, string Value)[] inputs) =>
        new(InstanceId.From("wf-000000000001"), _sha, inputs.ToDictionary(i => i.Name, i => i.Value, StringComparer.Ordinal));
}
