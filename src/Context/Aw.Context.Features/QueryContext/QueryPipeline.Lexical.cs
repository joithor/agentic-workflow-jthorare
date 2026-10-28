using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    // The record store answering with no hit is not an answer of the catalog's sources: every one of them missing is still SND-CONTEXT-011.
    private static bool NothingAnswered(Searched searched) => searched.Answers.All(a => a.Source == RecordStoreSource.Id && a.Hits.Count == 0);

    private static Error NoSourceAnswered(IEnumerable<DegradedSource> degraded) =>
        ContextErrors.AllSourcesUnavailable.With($"no source answered ({string.Join(", ", degraded.Select(d => d.Source.Value))}); fix: sindri context sources status");

    /// <summary>
    /// The catalog's sources and, for a live query with terms, the record store's lexical leg as one more source (spec §6.1), under the audiences the approved
    /// profile gives the repos in scope. A policy that can't answer drops the leg and says so, as a degraded source.
    /// </summary>
    private async Task<(List<IContextSource> Sources, List<DegradedSource> Degraded)> WithRecordStore(List<IContextSource> catalogSources, (ContextSettings Settings, IReadOnlyList<ContextRepo> Repos) profile,
        (DateTimeOffset? AsOf, IReadOnlyList<string> Terms) ask, CancellationToken ct)
    {
        var degraded = new List<DegradedSource>();
        if (ask.AsOf is not null || ask.Terms.Count == 0)
        {
            return (catalogSources, degraded);   // the store keeps no past: a backtest arm reads the sources alone
        }

        var policy = new ProfileAudiencePolicy(profile.Settings, [.. profile.Repos.Select(r => r.Name)]);
        var allowed = new HashSet<AudienceKey>();
        foreach (var repo in profile.Repos)
        {
            var scope = await policy.For(repo.Name, ct).ConfigureAwait(false);
            if (scope.IsError)
            {
                degraded.Add(SourceCalls.Degraded(RecordStoreSource.Id, scope.Error, clock.GetUtcNow()));
                return (catalogSources, degraded);
            }

            allowed.UnionWith(scope.Value.Allowed);
        }

        return ([.. catalogSources, new RecordStoreSource(lexical.Leg, lexical.Store, new AudienceScope(allowed), clock)], degraded);
    }
}
