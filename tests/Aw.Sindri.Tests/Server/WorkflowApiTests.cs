using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>The workflow routes on a real loopback port with real adapters in a temp home: start, signal and show over HTTP, and how an instance id binds from the route.</summary>
[Collection(nameof(ProcessEnvironment))]
public sealed class WorkflowApiTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task Start_signal_and_show_a_workflow_over_the_api()
    {
        await using var app = await Started();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var started = await Post(http, "/sindri/workflows", """{"recipe":"workflow.step","item":"ITEM-9"}""");
        var id = started.RootElement.GetProperty("instance").GetProperty("id").GetString();
        using var signalled = await Post(http, $"/sindri/workflows/{id}/signal", """{"on":"produced"}""");
        using var shown = JsonDocument.Parse(await http.GetStringAsync(new Uri($"/sindri/workflows/{id}", UriKind.Relative), Token));

        Assert.Equal("produce", started.RootElement.GetProperty("instance").GetProperty("state").GetString());
        Assert.Equal("checks", signalled.RootElement.GetProperty("instance").GetProperty("state").GetString());
        Assert.Equal("active", shown.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, shown.RootElement.GetProperty("history").GetArrayLength());
        await app.StopAsync(Token);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_well_formed_unknown_instance_is_404_SND_WF_004_and_malformed_text_is_400()
    {
        await using var app = await Started();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var unknown = await http.GetAsync(new Uri("/sindri/workflows/wf-000000000000", UriKind.Relative), Token);
        var malformed = await http.GetAsync(new Uri("/sindri/workflows/BAD", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains("SND-WF-004", await unknown.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.DoesNotContain("   at ", await malformed.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        await app.StopAsync(Token);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Every_instance_route_binds_the_id_from_the_route_through_TryParse()
    {
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await app.StartAsync(Token);

        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.Contains("{instance}", StringComparison.Ordinal) == true).ToList();

        Assert.Equal(9, routes.Count);
        Assert.All(routes, e => Assert.Contains(e.Metadata.OfType<IParameterBindingMetadata>(), m => m.Name == "instance" && m.HasTryParse));
        await app.StopAsync(Token);
    }

    private async Task<WebApplication> Started()
    {
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var started = await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), Token);
            Assert.True(started.IsSuccess);
        }

        await app.StartAsync(Token);
        return app;
    }

    private static async Task<JsonDocument> Post(HttpClient http, string path, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        var answer = await http.PostAsync(new Uri(path, UriKind.Relative), body, Token);
        var text = await answer.Content.ReadAsStringAsync(Token);
        Assert.True(answer.IsSuccessStatusCode, text);
        return JsonDocument.Parse(text);
    }
}
