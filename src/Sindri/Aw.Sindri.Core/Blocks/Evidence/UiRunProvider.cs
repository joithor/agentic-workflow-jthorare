using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>
/// The 'ui-browser' and 'design-parity' evidence providers: 'ui-evidence run' or 'parity' with the request's inputs, the
/// request's sha as the app build. The digest is the written document's.
/// </summary>
internal sealed class UiRunProvider : IEvidenceProvider
{
    private readonly IUiEvidence _tool;
    private readonly bool _parity;

    private UiRunProvider(BlockSpec spec, IUiEvidence tool, bool parity)
    {
        Spec = spec;
        _tool = tool;
        _parity = parity;
    }

    /// <inheritdoc/>
    public BlockSpec Spec { get; }

    /// <summary>'evidence.ui-browser': a UI script run headless in every viewport (summary.json).</summary>
    /// <param name="tool">ui-evidence.</param>
    /// <returns>The provider.</returns>
    internal static UiRunProvider UiBrowser(IUiEvidence tool) => new(Declare("ui-browser", "script", "A ui-evidence run of a UI script, every step passed (summary.json)"), tool, parity: false);

    /// <summary>'evidence.design-parity': the design frames measured against the app (parity.json).</summary>
    /// <param name="tool">ui-evidence.</param>
    /// <returns>The provider.</returns>
    internal static UiRunProvider DesignParity(IUiEvidence tool) => new(Declare("design-parity", "manifest", "A ui-evidence parity run, every geometry check passed (parity.json)"), tool, parity: true);

    /// <inheritdoc/>
    public async ValueTask<Result<ProvidedEvidence>> Provide(EvidenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ProviderInputs.Problem(Spec, request.Inputs) is { } problem)
        {
            return problem;
        }

        var input = Spec.Inputs[0].Name;
        var run = new UiEvidenceRun(request.Inputs[input], request.Inputs["run-dir"], request.Inputs["host"], request.Sha);
        var document = _parity
            ? await _tool.Parity(run, cancellationToken).ConfigureAwait(false)
            : await _tool.Run(run, cancellationToken).ConfigureAwait(false);
        if (document.IsError)
        {
            return document.Error;
        }

        var d = document.Value;
        var what = _parity ? "every geometry check passed" : "every step passed";
        var detail = d.Passed ? $"{Spec.Id.Value}: {what} ({d.Path})" : $"{Spec.Id.Value}: a check failed, see {d.Path}";
        return new ProvidedEvidence(d.Passed, d.Digest, d.Path, detail);
    }

    private static BlockSpec Declare(string kind, string input, string summary) =>
        new(BlockId.From($"evidence.{kind}"), BlockKind.Evidence, summary,
            [new(input, PortType.Text), new("run-dir", PortType.Text), new("host", PortType.Text)],
            [new($"{kind}.verdict", PortType.Verdict), new($"{kind}.document", PortType.Text)]);
}
