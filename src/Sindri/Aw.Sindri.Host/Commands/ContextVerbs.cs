using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Features.QueryContext;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context query': a client of POST /sindri/context/query. Exit 0 with results (a failed source only degrades
/// them), 1 with none or for a refused question or option (SND-CONTEXT-003), 2 for a setup problem (not running, no
/// approved profile, no source on).
/// </summary>
internal sealed class ContextVerbs(ControlPlaneClient client, Output output)
{
    internal async Task<int> Query(string[] args, CancellationToken ct)
    {
        var parsed = Parse(args);
        if (parsed.IsError)
        {
            return await Refuse(parsed.Error).ConfigureAwait(false);
        }

        var (request, json) = parsed.Value;
        var answer = await client.Post<QueryContextRequest, QueryContextResponse>("/sindri/context/query", request, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error.Code == SindriErrors.ContextBadQuery.Code ? await Refuse(answer.Error).ConfigureAwait(false) : await Fail(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Results.Count == 0 ? 1 : 0;
        return json ? await output.Json(answer.Value, code).ConfigureAwait(false) : await output.Text(Render(answer.Value), code).ConfigureAwait(false);
    }

    internal static Result<(QueryContextRequest Request, bool Json)> Parse(string[] args)
    {
        string? question = null;
        IReadOnlyList<string>? sources = null;
        int? limit = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--sources" when i + 1 < args.Length:
                    sources = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--limit" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var n):
                    limit = n;
                    i++;
                    break;
                case var word when question is null && !word.StartsWith("--", StringComparison.Ordinal):
                    question = word;
                    break;
                default:
                    return SindriErrors.ContextBadQuery.With($"'{args[i]}' isn't a context query argument (fix: sindri context query \"<question>\" [--sources code,notes] [--limit N] [--json])");
            }
        }

        return question is null
            ? SindriErrors.ContextBadQuery.With("no question (fix: sindri context query \"<question>\")")
            : (new QueryContextRequest(question, sources, limit), json);
    }

    internal static string Render(QueryContextResponse answer)
    {
        var text = new StringBuilder();
        _ = text.Append(CultureInfo.InvariantCulture, $"Context for \"{answer.Question}\" (terms: {string.Join(", ", answer.Terms)})\n");
        if (answer.Degraded)
        {
            _ = text.Append("DEGRADED: a source didn't answer; results may miss what it holds\n");
        }

        _ = answer.Results.Count == 0 ? text.Append("no results\n") : text;
        foreach (var hit in answer.Results)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{hit.Rank,2}. [{SourceNames.Of(hit.Cite.Source)}] {hit.Cite.Locator}{(hit.Redacted ? " (redacted)" : string.Empty)}\n")
                .Append(CultureInfo.InvariantCulture, $"    {hit.Snippet}\n")
                .Append(CultureInfo.InvariantCulture, $"    retrieved {hit.Cite.RetrievedAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\n");
        }

        var sources = answer.Sources.Select(s => s.State switch
        {
            SourceState.Ok => string.Create(CultureInfo.InvariantCulture, $"{SourceNames.Of(s.Source)} ok ({s.Hits} hits)"),
            SourceState.Skipped => $"{SourceNames.Of(s.Source)} skipped ({s.Detail})",
            SourceState.Failed => $"{SourceNames.Of(s.Source)} failed ({s.Detail})",
            _ => $"{SourceNames.Of(s.Source)} {s.State}",
        });
        return text.Append("sources: ").AppendJoin("; ", sources).Append('\n').ToString();
    }

    private async Task<int> Refuse(Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> Fail(Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return 2;
    }
}
