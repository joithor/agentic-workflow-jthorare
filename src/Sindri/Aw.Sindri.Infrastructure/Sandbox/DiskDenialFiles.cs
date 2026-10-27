using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The denial counters on disk (sandbox spec §3.6). A launch's stored plan, '&lt;home&gt;/launches/&lt;id&gt;.sandbox.json', names its run area through the
/// AW_SANDBOX_PLAN environment entry (the plan file sits in the run area); the counter is 'denials.json' next to it. A run area the sweep has removed
/// has no counter, so the ledger's 'sandbox.denied' events are the history and these files are the live count. An unreadable file is skipped, not an error.
/// </summary>
/// <param name="settings">Sindri's home.</param>
/// <param name="options">The package's AwJson options.</param>
internal sealed class DiskDenialFiles(SindriSettings settings, JsonSerializerOptions options) : IDenialFiles
{
    private const string PlanSuffix = ".sandbox.json";
    private const string CounterName = "denials.json";

    public async ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(settings.Home, "launches");
        string[] plans;
        try
        {
            plans = launch is { } one
                ? [Path.Combine(directory, one.Value + PlanSuffix)]
                : [.. Directory.EnumerateFiles(directory, "*" + PlanSuffix)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];   // boundary: no launches directory yet means nothing was launched
        }

        var found = new List<DenialFile>();
        foreach (var plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ReadCounter(plan, cancellationToken).ConfigureAwait(false) is { } file)
            {
                found.Add(file);
            }
        }

        return [.. found.OrderByDescending(static f => f.UpdatedAt).ThenBy(static f => f.LaunchId.Value, StringComparer.Ordinal)];
    }

    private async Task<DenialFile?> ReadCounter(string planPath, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await File.ReadAllBytesAsync(planPath, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(plan);
            var area = RunArea(document.RootElement);
            if (area is null)
            {
                return null;
            }

            var counter = await File.ReadAllBytesAsync(Path.Combine(area, CounterName), cancellationToken).ConfigureAwait(false);
            var parsed = AwJson.Deserialize<DenialFile>(counter, options);
            return parsed.IsSuccess ? parsed.Value : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;   // boundary: a plan or counter that is gone or unreadable has no count to show
        }
    }

    private static string? RunArea(JsonElement plan)
    {
        if (plan.ValueKind != JsonValueKind.Object || !plan.TryGetProperty("environment", out var environment) || environment.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in environment.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("name", out var name) && string.Equals(name.GetString(), SandboxEnvironment.Plan, StringComparison.Ordinal)
                && entry.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            {
                return Path.GetDirectoryName(value.GetString());
            }
        }

        return null;
    }
}
