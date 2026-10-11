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
/// approved profile, no source on) or when nothing could be searched (no results and no source answered: each failed or
/// couldn't open).
/// </summary>
internal sealed class ContextVerbs(ControlPlaneClient client, Output output)
{
    internal async Task<int> Query(string[] args, CancellationToken ct)
    {
        var parsed = Parse(args);
        if (parsed.IsError)
        {
            _ = await output.Error(parsed.Error).ConfigureAwait(false);
            return 1;
        }

        var (request, json) = parsed.Value;
        var answer = await client.Post<QueryContextRequest, QueryContextResponse>("/sindri/context/query", request, ct).ConfigureAwait(false);
        var code = ExitCode(answer);
        if (answer.IsError)
        {
            _ = await output.Error(answer.Error).ConfigureAwait(false);
            return code;
        }

        return json ? await output.Json(answer.Value, code).ConfigureAwait(false) : await output.Text(Render(answer.Value), code).ConfigureAwait(false);
    }

    /// <summary>The exit code for an answer: 0 results; 1 none (some source answered), or a refused query; 2 a setup problem, or no source answered.</summary>
    internal static int ExitCode(Result<QueryContextResponse> answer) => answer switch
    {
        { IsError: true } when answer.Error.Code == SindriErrors.ContextBadQuery.Code => 1,
        { IsError: true } => 2,
        _ when answer.Value.Results.Count > 0 => 0,
        _ when !answer.Value.Sources.Any(s => s.State == SourceState.Ok) => 2,
        _ => 1,
    };

    internal static Result<(QueryContextRequest Request, bool Json)> Parse(string[] args)
    {
        string? question = null;
        string[]? sources = null;
        int? limit = null;
        var json = false;
        for (var i = 0; i < args.Length; i++)
        {
            var problem = args[i] switch
            {
                "--json" => Set(() => json = true),
                "--sources" => Value(args, ++i, SourceList, v => sources = v),
                "--limit" => Value(args, ++i, Limit, v => limit = v),
                var word when question is null && !word.StartsWith("--", StringComparison.Ordinal) => Set(() => question = word),
                _ => SindriErrors.ContextBadQuery.With($"'{args[i]}' isn't a context query argument (fix: sindri context query \"<question>\" [--sources code,notes] [--limit N] [--json])"),
            };
            if (problem is not null)
            {
                return problem;
            }
        }

        return question is null
            ? SindriErrors.ContextBadQuery.With("no question (fix: sindri context query \"<question>\")")
            : (new QueryContextRequest(question, sources, limit), json);
    }

    private static Error? Set(Action set)
    {
        set();
        return null;
    }

    private static Error? Value<T>(string[] args, int at, Func<string?, Result<T>> parse, Action<T> set)
    {
        var parsed = parse(at < args.Length ? args[at] : null);
        if (parsed.IsError)
        {
            return parsed.Error;
        }

        set(parsed.Value);
        return null;
    }

    private static Result<string[]> SourceList(string? value) =>
        value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } names
            ? names
            : SindriErrors.ContextBadQuery.With($"--sources needs one or more source names (fix: --sources {SourceNames.All.Replace(", ", ",", StringComparison.Ordinal)})");

    private static Result<int> Limit(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : SindriErrors.ContextBadQuery.With($"--limit needs a whole number (fix: --limit 1 to {Core.Profile.ContextSettings.MaxLimit})");

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
}
