using System.Net;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

/// <summary>
/// Linear's GraphQL endpoint in memory, behind HttpClient: it reads the request the real adapter sends, searches the
/// seeded issues and answers in Linear's shape. No network, and no recorded live call.
/// </summary>
internal sealed class FakeLinearHandler(IReadOnlyList<SeedDocument> issues, JsonSerializerOptions json) : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public string? GraphQlError { get; set; }

    public List<(LinearSearchRequest Body, string? Authorization)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = AwJson.Deserialize<LinearSearchRequest>(await request.Content!.ReadAsByteArrayAsync(cancellationToken), json).Value;   // why: the adapter always sends a body
        Requests.Add((body, request.Headers.TryGetValues("Authorization", out var values) ? values.Single() : null));
        var terms = body.Variables.Term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var answer = GraphQlError is { } error
            ? new LinearSearchAnswer(Errors: [new LinearError(error)])
            : new LinearSearchAnswer(new LinearSearchData(new LinearIssuePage(
                [.. FakeSearch.Run(issues, terms, body.Variables.First).Select(m => new LinearIssue(m.Document.Name.ToUpperInvariant(), m.Document.Title, "https://linear.app/team/issue/" + m.Document.Name, m.Document.Body))])));
        return new HttpResponseMessage(Status) { Content = new ByteArrayContent(AwJson.Serialize(answer, json)) };
    }
}
