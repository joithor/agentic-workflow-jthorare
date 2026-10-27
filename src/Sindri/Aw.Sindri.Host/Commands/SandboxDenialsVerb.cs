using System.Globalization;
using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ListDenials;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox denials [--run &lt;launchId&gt;] [--since &lt;time&gt;] [--json]' (sandbox spec §3.6): what the sandbox blocked, grouped by target and rule with the fix each kind allows.
/// A query through the running control plane, which never starts it: exit 1 SND-API-001 when it is down. '--since' is a time (2026-10-27T09:00:00Z) or an age (30m, 6h, 2d).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="clock">The clock, for an age.</param>
internal sealed class SandboxDenialsVerb(ControlPlaneClient client, Output output, TimeProvider clock)
{
    internal const string Usage = "usage: sindri sandbox denials [--run <launchId>] [--since <time|30m|6h|2d>] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        string? run = null, since = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--run" when i + 1 < args.Length:
                    if (!LaunchId.TryParse(args[i + 1], provider: null, out _))
                    {
                        return await Invalid($"'{args[i + 1]}' isn't a launch id (LCH-…)").ConfigureAwait(false);
                    }

                    run = args[++i];
                    break;
                case "--since" when i + 1 < args.Length:
                    since = args[++i];
                    break;
                default:
                    return await Invalid($"'{args[i]}' isn't an argument of this command, or it needs a value").ConfigureAwait(false);
            }
        }

        string? sinceAt = null;
        if (since is not null)
        {
            if (!TryTime(since, out var at))
            {
                return await Invalid($"--since needs a time such as 2026-10-27T09:00:00Z or an age such as 6h, not '{since}'").ConfigureAwait(false);
            }

            sinceAt = at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        var query = new List<string>();
        if (run is not null)
        {
            query.Add("launch=" + Uri.EscapeDataString(run));
        }

        if (sinceAt is not null)
        {
            query.Add("since=" + Uri.EscapeDataString(sinceAt));
        }

        var answer = await client.Get<ListDenialsResponse>("/sindri/sandbox/denials" + (query.Count == 0 ? string.Empty : "?" + string.Join('&', query)), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(DenialsRenderer.Render(answer.Value.Value.Launches), 0).ConfigureAwait(false);
    }

    private bool TryTime(string text, out DateTimeOffset at)
    {
        if (text.Length >= 2 && text[..^1] is var digits && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            TimeSpan? age = text[^1] switch
            {
                'm' => TimeSpan.FromMinutes(n),
                'h' => TimeSpan.FromHours(n),
                'd' => TimeSpan.FromDays(n),
                _ => null,
            };
            if (age is { } span)
            {
                at = clock.GetUtcNow() - span;
                return true;
            }
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out at);
    }

    private async Task<int> Invalid(string message) =>
        await output.Error(SindriErrors.InvalidInput.With($"{message} ({Usage})")).ConfigureAwait(false);
}
