using System.Net;
using System.Net.Http.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.QueryContext;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Context;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.QueryContext;

/// <summary>POST /sindri/context/query on the real host on loopback, over real adapters and a real notes directory.</summary>
[Collection(nameof(ProcessEnvironment))]
public sealed class ContextApiTests : IDisposable
{
    private readonly TempTree _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_route_answers_cited_scrubbed_results_and_refuses_a_bad_question_and_a_foreign_origin()
    {
        var notes = Path.Combine(_home.Path, "notes");
        _ = _home.Write("notes/lease.md", "# Lease\nThe host lease has an epoch.\n");
        _ = _home.Write("notes/deploy.md", $"# Deploy\nUse the token {Testing.Context.TextScrubberContract.PlantedToken} for the lease host.\n");
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await Approve(app.Services, new ContextSettings(Notes: new NotesContext(notes)));
        await app.StartAsync(Token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var ok = await http.PostAsJsonAsync("/sindri/context/query", new QueryContextRequest("host lease"), HttpJson.Info<QueryContextRequest>(), Token);
        var answer = await ok.Content.ReadFromJsonAsync(HttpJson.Info<QueryContextResponse>(), Token);
        var bad = await http.PostAsJsonAsync("/sindri/context/query", new QueryContextRequest("a"), HttpJson.Info<QueryContextRequest>(), Token);
        using var foreign = new HttpRequestMessage(HttpMethod.Post, "/sindri/context/query") { Content = JsonContent.Create(new QueryContextRequest("lease"), HttpJson.Info<QueryContextRequest>()) };
        foreign.Headers.Add("Origin", "http://evil.example");
        var refused = await http.SendAsync(foreign, Token);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(2, answer!.Results.Count);
        Assert.All(answer.Results, r => Assert.Equal(SourceKind.Notes, r.Cite.Source));
        Assert.DoesNotContain(answer.Results, r => r.Snippet.Contains(Testing.Context.TextScrubberContract.PlantedToken, StringComparison.Ordinal));
        Assert.Contains(answer.Results, r => r.Redacted && r.Snippet.Contains("[REDACTED:", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains(SindriErrors.ContextBadQuery.Code, await bad.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        await app.StopAsync(Token);
    }

    private static async Task Approve(IServiceProvider services, ContextSettings context)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Context: context)).Value;
        var json = services.GetRequiredService<System.Text.Json.JsonSerializerOptions>();
        _ = await services.GetRequiredService<IApprovals>().Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, json)), "test"), Token);
    }
}
