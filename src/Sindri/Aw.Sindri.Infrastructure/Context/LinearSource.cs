using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Context;

/// <summary>
/// The Linear source: read-only GraphQL searchIssues over the issues the key can read (optionally some teams), in
/// Linear's own rank order. Only the search terms leave the machine; what comes back is never stored. The key is sent as
/// Linear's personal-key Authorization header and never appears in a message.
/// </summary>
internal sealed class LinearSource(HttpClient http, Uri endpoint, string key, LinearContext settings, JsonSerializerOptions json) : IContextSource
{
    internal static readonly Uri Api = new("https://api.linear.app/graphql");

    private const string AllTeams =
        "query Search($term: String!, $first: Int!) { searchIssues(term: $term, first: $first) { nodes { identifier title url description } } }";

    private const string SomeTeams =
        "query Search($term: String!, $first: Int!, $teams: [String!]) { searchIssues(term: $term, first: $first, filter: { team: { key: { in: $teams } } }) { nodes { identifier title url description } } }";

    public SourceKind Kind => SourceKind.Linear;

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        var teams = settings.Teams is { Count: > 0 } named ? named : null;
        var body = new LinearSearchRequest(teams is null ? AllTeams : SomeTeams, new LinearSearchVariables(string.Join(' ', query.Terms), query.Limit, teams));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(AwJson.Serialize(body, json)) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            _ = request.Headers.TryAddWithoutValidation("Authorization", key);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? Hits(AwJson.Deserialize<LinearSearchAnswer>(bytes, json), query)
                : SindriErrors.ContextSourceFailed.With(string.Create(CultureInfo.InvariantCulture, $"Linear answered HTTP {(int)response.StatusCode}"));
        }
        catch (OperationCanceledException)
        {
            return SindriErrors.ContextSourceFailed.With("the Linear search was cancelled or timed out");
        }
        catch (HttpRequestException e)
        {
            return SindriErrors.ContextSourceFailed.With($"can't reach Linear: {e.Message}");
        }
    }

    private static Result<IReadOnlyList<SourceHit>> Hits(Result<LinearSearchAnswer> answer, SourceQuery query)
    {
        if (answer.IsError)
        {
            return SindriErrors.ContextSourceFailed.With($"Linear's answer didn't read: {answer.Error.Message}");
        }

        if (answer.Value is { Errors: { Count: > 0 } errors })
        {
            return SindriErrors.ContextSourceFailed.With($"Linear returned errors: {string.Join("; ", errors.Select(e => e.Message))}");
        }

        var issues = answer.Value.Data?.SearchIssues.Nodes ?? [];
        return Results.Success<IReadOnlyList<SourceHit>>(
            [.. issues.Take(query.Limit).Select(i => new SourceHit(i.Url, Line: null, Snippets.Raw($"{i.Identifier} {i.Title}"), Snippets.Around(i.Description is null ? i.Title : i.Description + "\n" + i.Title, query.Terms)))]);
    }
}
