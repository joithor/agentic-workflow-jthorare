using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Onyx;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The inline approval `onyx up` asks for, against a real control plane on loopback: the same preview and the same typed challenge as `profile approve`.</summary>
public sealed class OnyxProfileApproverTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _home.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task The_preview_then_the_typed_challenge_approves_the_live_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var hash = (await app.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;
        await using var scope = app.Services.CreateAsyncScope();
        using var client = Client(app);

        var approved = await Approver(app, client, scope.ServiceProvider, "approve profile " + hash.Abbreviated + "\n").Approve(ct);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.Message : "");
        Assert.True(approved.Value.Approved);
        Assert.Equal(hash, approved.Value.Hash);
        var shown = _out.ToString();
        Assert.Contains(hash.Value, shown, StringComparison.Ordinal);   // the preview named it
        Assert.Contains("Type 'approve profile " + hash.Abbreviated + "'", shown, StringComparison.Ordinal);   // and the challenge asked for the phrase
        await app.StopAsync(ct);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Typing_nothing_is_a_decline_SND_PROFILE_004_and_approves_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        await using var scope = app.Services.CreateAsyncScope();
        using var client = Client(app);

        var declined = await Approver(app, client, scope.ServiceProvider, string.Empty).Approve(ct);

        Assert.Equal("SND-PROFILE-004", declined.Error.Code);
        await app.StopAsync(ct);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_profile_that_is_already_approved_is_returned_without_a_prompt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        var hash = (await app.Services.GetRequiredService<IProfileSource>().Load(ct)).Value.Hash;
        await using var scope = app.Services.CreateAsyncScope();
        using var client = Client(app);
        _ = await Approver(app, client, scope.ServiceProvider, "approve profile " + hash.Abbreviated + "\n").Approve(ct);
        _out.GetStringBuilder().Clear();

        var again = await Approver(app, client, scope.ServiceProvider, string.Empty).Approve(ct);

        Assert.True(again.Value.Approved);
        Assert.DoesNotContain("Type '", _out.ToString(), StringComparison.Ordinal);
        await app.StopAsync(ct);
    }

    private async Task<WebApplication> Started(CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        LeaseGrant grant;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold((await app.Services.GetRequiredService<IHumanTokenStore>().Mint(ct)).Value);
        await app.StartAsync(ct);
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        return app;
    }

    private static ControlPlaneClient Client(WebApplication app) =>
        new(app.Services.GetRequiredService<IControlPlaneProbe>(), SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Signal: null, Terminal: true, Prover: false), app.Services.GetRequiredService<IHumanTokenStore>(), static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));

    private ProfileApprover Approver(WebApplication app, ControlPlaneClient client, IServiceProvider scope, string typed)
    {
        var output = new Output(_out, _error, SliceHarness.Json);
        return new ProfileApprover(new Verbs(client, app.Services.GetRequiredService<IControlPlaneProbe>(), output, scope), output, new StringReader(typed));
    }
}
