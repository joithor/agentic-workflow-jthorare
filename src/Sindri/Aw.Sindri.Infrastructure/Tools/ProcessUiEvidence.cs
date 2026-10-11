using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Evidence;

namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>
/// ui-evidence through its CLI contract (docs/csharp/evidence.md). doctor: exit 0 healthy, 2 not, the document on stdout
/// either way. provenance: exit 0 and its document. run and parity: exit 0 every check passed, 2 one failed, the document
/// written to the run directory either way (its bytes are hashed for the digest). Any other exit (1 usage or a guard, 4 the
/// run errored, 75 a lock stayed busy, 130 interrupted) or a document that doesn't read is SND-BLK-001 with ui-evidence's line.
/// </summary>
/// <param name="settings">Where the ui-evidence binary is.</param>
/// <param name="json">The package's AwJson options (the documents are in its context).</param>
internal sealed class ProcessUiEvidence(ToolSettings settings, JsonSerializerOptions json) : IUiEvidence
{
    public async ValueTask<Result<StackHealth>> Doctor(CancellationToken cancellationToken)
    {
        var run = await Call(["doctor"], ToolSettings.ProbeTimeout, cancellationToken).ConfigureAwait(false);
        return run.IsError ? run.Error : Document<StackHealth>("doctor", run.Value, 0, 2);
    }

    public async ValueTask<Result<DatabaseProvenance>> Provenance(CancellationToken cancellationToken)
    {
        var run = await Call(["provenance"], ToolSettings.ProbeTimeout, cancellationToken).ConfigureAwait(false);
        return run.IsError ? run.Error : Document<DatabaseProvenance>("provenance", run.Value, 0);
    }

    public ValueTask<Result<EvidenceDocument>> Run(UiEvidenceRun run, CancellationToken cancellationToken) =>
        Measure("run", "summary.json", run, cancellationToken);

    public ValueTask<Result<EvidenceDocument>> Parity(UiEvidenceRun run, CancellationToken cancellationToken) =>
        Measure("parity", "parity.json", run, cancellationToken);

    private async ValueTask<Result<EvidenceDocument>> Measure(string command, string file, UiEvidenceRun run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        var runDirectory = Path.GetFullPath(run.RunDirectory);
        var result = await Call([command, Path.GetFullPath(run.Input), runDirectory, "--host", run.Host, "--app-build", run.AppBuild.Value],
            ToolSettings.RunTimeout, ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return result.Error;
        }

        if (result.Value.Exit is not (0 or 2))
        {
            return Failed(command, result.Value);
        }

        var path = Path.Combine(runDirectory, file);
        if (!File.Exists(path))
        {
            return SindriErrors.ToolUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"ui-evidence {command} exited {result.Value.Exit} but wrote no {path}"));
        }

        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false)));
        return new EvidenceDocument(result.Value.Exit == 0, path, digest);
    }

    private Result<T> Document<T>(string command, ToolRun run, params int[] exits)
    {
        if (!exits.Contains(run.Exit))
        {
            return Failed(command, run);
        }

        var document = AwJson.Deserialize<T>(run.Stdout, json);
        return document.IsError
            ? SindriErrors.ToolUnavailable.With($"ui-evidence {command} printed no readable document: {document.Error.Message}")
            : document;
    }

    private static Error Failed(string command, ToolRun run) =>
        SindriErrors.ToolUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"ui-evidence {command} exited {run.Exit}: {run.Message}"));

    private ValueTask<Result<ToolRun>> Call(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct) =>
        ToolProcess.Run(settings.UiEvidenceBin, arguments, stdin: null, timeout, ct);
}
