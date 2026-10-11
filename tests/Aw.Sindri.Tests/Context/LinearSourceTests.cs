using System.Net;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

public sealed class LinearSourceTests : IDisposable
{
    private static readonly IReadOnlyList<SeedDocument> _issues = [new("eng-1", "Lease epoch fencing", "Fence every write by the epoch.")];
    private readonly FakeLinearHandler _linear = new(_issues, SliceHarness.Json);
    private readonly HttpClient _http;

    public LinearSourceTests()
    {
        _http = new HttpClient(_linear);
    }

    public void Dispose() => _http.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task It_sends_the_key_the_terms_and_the_teams_and_reads_issues_as_cited_hits()
    {
        var source = new LinearSource(_http, LinearSource.Api, "lin_api_key", new LinearContext(["ENG"]), SliceHarness.Json);

        var hit = Assert.Single((await source.Search(new SourceQuery(["lease", "epoch"], 5), Token)).Value);

        var (body, authorization) = Assert.Single(_linear.Requests);
        Assert.Equal("lin_api_key", authorization);
        Assert.Equal(("lease epoch", 5), (body.Variables.Term, body.Variables.First));
        Assert.Equal(["ENG"], body.Variables.Teams!);   // why: the source was given teams
        Assert.Contains("$teams", body.Query, StringComparison.Ordinal);
        Assert.Equal(("https://linear.app/team/issue/eng-1", "ENG-1 Lease epoch fencing"), (hit.Document, hit.Title));
        Assert.Null(hit.Line);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_teams_it_searches_every_team()
    {
        var source = new LinearSource(_http, LinearSource.Api, "k", new LinearContext(), SliceHarness.Json);

        _ = await source.Search(new SourceQuery(["lease"], 5), Token);

        var (body, _) = Assert.Single(_linear.Requests);
        Assert.Null(body.Variables.Teams);
        Assert.DoesNotContain("$teams", body.Query, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.Unauthorized, null, "Linear answered HTTP 401")]
    [InlineData(HttpStatusCode.OK, "Argument Validation Error", "Linear returned errors: Argument Validation Error")]
    public async Task A_refusal_is_a_failed_source_that_never_shows_the_key(HttpStatusCode status, string? graphQlError, string expected)
    {
        _linear.Status = status;
        _linear.GraphQlError = graphQlError;
        var source = new LinearSource(_http, LinearSource.Api, "lin_api_secret_value", new LinearContext(), SliceHarness.Json);

        var error = (await source.Search(new SourceQuery(["lease"], 5), Token)).Error;

        Assert.Equal(SindriErrors.ContextSourceFailed.Code, error.Code);
        Assert.Equal(expected, error.Message);
        Assert.DoesNotContain("lin_api_secret_value", error.Message, StringComparison.Ordinal);
    }
}
