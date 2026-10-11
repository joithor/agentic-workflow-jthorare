using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.QueryContext;

/// <summary>
/// The query planner (factory addendum §17.3, §17.29.2). The question is scrubbed first, so a pasted secret never becomes
/// a term sent to a source. Its terms go to every source the approved profile turns on, in parallel, each with its own
/// deadline; the answers are fused by rank, every text field is scrubbed before it is cut for display, and every result
/// is cited. A source that fails or runs late degrades the answer instead of failing it.
/// </summary>
internal sealed class QueryContextService(IApprovals approvals, ISourceCatalog catalog, ITextScrubber scrubber, TimeProvider clock)
    : IService<QueryContextRequest, QueryContextResponse>
{
    private static readonly string _seconds = string.Create(CultureInfo.InvariantCulture, $"{ContextQueryDefaults.Deadline.TotalSeconds:0}s");

    public async ValueTask<Result<QueryContextResponse>> Handle(QueryContextRequest request, CancellationToken cancellationToken)
    {
        var question = scrubber.Scrub(request.Question).Text;
        var terms = QueryTerms.From(question);
        if (terms.IsError)
        {
            return terms.Error;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var plan = approved.IsError ? approved.Error : Plan(approved.Value, request);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var (openings, limit) = plan.Value;
        var query = new SourceQuery(terms.Value, limit);
        var answers = await Task.WhenAll(openings.Select(o => Ask(o, query, cancellationToken))).ConfigureAwait(false);
        var fused = RankFusion.Fuse([.. answers.Select(a => a.List).OfType<SourceList>()], limit);
        return new QueryContextResponse(question, terms.Value, [.. fused.Select((f, i) => Cite(f, i + 1))], [.. answers.Select(a => a.Report)]);
    }

    private Result<(IReadOnlyList<SourceOpening> Openings, int Limit)> Plan(LoadedProfile? approved, QueryContextRequest request)
    {
        if (approved is null)
        {
            return SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        var named = NamedSources.In(approved.Profile);
        if (approved.Profile.Context is not { } context || named.Count == 0)
        {
            return SindriErrors.ContextNoSources.With("the approved profile turns on no context source (fix: add a context: section naming code, notes, linear or connectai, then approve it)");
        }

        var limit = request.Limit ?? context.Limit;
        if (limit is < 1 or > ContextSettings.MaxLimit)
        {
            return SindriErrors.ContextBadQuery.With(string.Create(CultureInfo.InvariantCulture,
                $"limit {limit} is out of range (fix: ask for 1 to {ContextSettings.MaxLimit} results)"));
        }

        var asked = Asked(request.Sources);
        if (asked.IsError)
        {
            return asked.Error;
        }

        return (Narrow(catalog.Open(approved.Profile), named, asked.Value), limit);
    }

    private static Result<IReadOnlySet<SourceKind>?> Asked(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0)
        {
            return Results.Success<IReadOnlySet<SourceKind>?>(value: null);
        }

        var kinds = new HashSet<SourceKind>();
        foreach (var name in names)
        {
            if (!SourceNames.TryParse(name, out var kind))
            {
                return SindriErrors.ContextBadQuery.With($"'{name}' is not a context source (fix: use {SourceNames.All})");
            }

            _ = kinds.Add(kind);
        }

        return kinds;
    }

    /// <summary>The openings to ask: all of them, or those asked for; an asked source the profile doesn't turn on is reported, never silently absent.</summary>
    private static IReadOnlyList<SourceOpening> Narrow(IReadOnlyList<SourceOpening> openings, IReadOnlyList<SourceKind> named, IReadOnlySet<SourceKind>? asked)
    {
        if (asked is null)
        {
            return openings;
        }

        var notOn = asked.Where(k => !named.Contains(k)).Order().Select(k => SourceOpening.Skip(k,
            $"{SourceNames.Of(k)} is not on in the approved profile (fix: add context.{SourceNames.Of(k)} to the profile, then approve it)"));
        return [.. openings.Select(o => asked.Contains(o.Kind) ? o : new SourceOpening(o.Kind, Source: null, SourceOpening.NotAskedFor)), .. notOn];
    }

    private async Task<(SourceList? List, SourceReport Report)> Ask(SourceOpening opening, SourceQuery query, CancellationToken cancellationToken)
    {
        if (opening.Source is not { } source)
        {
            return (null, new SourceReport(opening.Kind, SourceState.Skipped, 0, opening.Skipped));
        }

        using var deadline = new CancellationTokenSource(ContextQueryDefaults.Deadline, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        Result<IReadOnlyList<SourceHit>> answer;
        try
        {
            // WaitAsync on the same token: a source that ignores cancellation is abandoned at the deadline, not awaited.
            answer = await source.Search(query, linked.Token).AsTask().WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            answer = SindriErrors.ContextSourceFailed.With($"it didn't answer within {_seconds}");
        }

        if (answer.IsError)
        {
            var late = deadline.IsCancellationRequested && !answer.Error.Message.Contains(_seconds, StringComparison.Ordinal) ? $" (past the {_seconds} deadline)" : string.Empty;
            var detail = scrubber.Scrub($"{answer.Error.Code} {answer.Error.Message}{late}").Text;
            return (null, new SourceReport(opening.Kind, SourceState.Failed, 0, detail));
        }

        var hits = answer.Value.Take(query.Limit).ToList();
        return (new SourceList(opening.Kind, hits, clock.GetUtcNow()), new SourceReport(opening.Kind, SourceState.Ok, hits.Count, Detail: null));
    }

    private ContextHit Cite(FusedHit fused, int rank)
    {
        var title = scrubber.Scrub(fused.Hit.Title);
        var snippet = scrubber.Scrub(fused.Hit.Snippet);
        var document = scrubber.Scrub(fused.Hit.Document);
        var shown = Snippets.Shown(snippet.Text);
        var withheld = string.Equals(shown, Snippets.EncodedBlob, StringComparison.Ordinal);
        return new ContextHit(rank, Snippets.Line(title.Text), shown, fused.Score, title.Redacted || snippet.Redacted || document.Redacted || withheld,
            new Citation(fused.Source, document.Text, fused.Hit.Line, fused.RetrievedAt));
    }
}
