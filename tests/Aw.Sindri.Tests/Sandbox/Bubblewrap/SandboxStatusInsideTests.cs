using System.Diagnostics;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Launch;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// SB-5a proof, the net-forward half end to end (sandbox spec §12 SB-5a): a started 'sindri serve' in a scratch AW_SINDRI_HOME, a real bubblewrap sandbox with the
/// launch's egress forwarders and session home, and 'sindri status' run inside it on a private tmux server. It must answer through the session home's control.json
/// and the inside forwarder, with no network but loopback. Heavy lane: it starts serve (Temporal) and needs bwrap, socat and the installed C# binaries.
/// </summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxStatusInsideTests
{
    [Fact]
    public async Task Sindri_status_inside_the_sandbox_answers_through_the_session_homes_control_json()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using var box = SandboxBox.Create(BubblewrapBox.FixtureParent, home);
        var sindri = Path.Combine(box.Host().CsBinDir, "sindri");
        Assert.SkipUnless(File.Exists(sindri), "no installed C# build: run scripts/install-cs.sh --link (the sandbox binds bin/cs/current)");
        using var tmux = new PrivateTmux();
        var ct = TestContext.Current.CancellationToken;

        using var serve = StartServe(sindri, box);
        try
        {
            var endpoint = await WaitForControl(box.SindriHome + "/control.json", serve, ct);
            var request = box.ReadOnly() with { Net = new SandboxNetFacts(box.NetDir, endpoint.Url.Port, 41234) };
            var sandbox = BubblewrapBox.Create();
            var plan = sandbox.Plan(request);
            Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);

            _ = SessionHome.Create(box.SindriHome, request.Launch, "secret", endpoint);
            await using var egress = StartEgress(plan.Value);
            var wrapped = sandbox.Wrap(plan.Value, [sindri, "status", "--json"]);
            Assert.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.ToString() : null);

            var output = Path.Combine(box.Root, "status.out");
            var code = Path.Combine(box.Root, "status.code");
            var line = string.Join(' ', wrapped.Value.Select(Quote)) + $" > {Quote(output)} 2>&1; echo $? > {Quote(code)}";
            Assert.NotNull(tmux.Run("new-session", "-d", "-s", "status-inside", "-c", box.Root, "sh", "-c", line));
            await WaitForFile(code, ct);

            var printed = await File.ReadAllTextAsync(output, ct);
            Assert.Equal("0", (await File.ReadAllTextAsync(code, ct)).Trim());
            Assert.Contains("apiVersion", printed, StringComparison.Ordinal);   // `sindri status --json` carries it (aot-smoke expected.txt)
        }
        finally
        {
            RemoveSessionHome(box);
            Stop(serve);
        }
    }

    private static void RemoveSessionHome(SandboxBox box) => SessionHome.Remove(SessionHomePaths.For(box.SindriHome, box.WriteScope().Launch));

    private static RunningEgress StartEgress(SandboxPlan plan)
    {
        var network = new SystemEgressNetwork();
        var started = new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(plan.RunDirectory, "egress")).Start(plan.Network, EgressCaps.Default);
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        return started.Value;
    }

    private static Process StartServe(string sindri, SandboxBox box)
    {
        var info = new ProcessStartInfo(sindri, ["serve"]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        _ = info.Environment.Remove("TMUX");
        info.Environment["AW_SINDRI_HOME"] = box.SindriHome;
        info.Environment["AW_STATE_DIR"] = box.State;
        info.Environment["AW_SINDRI_PORT"] = "0";
        return Process.Start(info) ?? throw new InvalidOperationException("sindri serve did not start");
    }

    private static async Task<ControlEndpoint> WaitForControl(string path, Process serve, CancellationToken ct)
    {
        for (var waited = 0; waited < 120; waited++)
        {
            Assert.False(serve.HasExited, "sindri serve exited early with code " + serve.ExitCode);
            if (File.Exists(path) && AwJson.Deserialize<ControlEndpoint>(await File.ReadAllBytesAsync(path, ct), SandboxFilesJson.Options) is { IsSuccess: true } read)
            {
                return read.Value;
            }

            await Task.Delay(500, ct);
        }

        throw new TimeoutException("sindri serve wrote no control.json in 60 s");
    }

    private static async Task WaitForFile(string path, CancellationToken ct)
    {
        for (var waited = 0; waited < 120 && !File.Exists(path); waited++)
        {
            await Task.Delay(500, ct);
        }

        Assert.True(File.Exists(path), "sindri status did not finish inside the sandbox in 60 s");
    }

    private static void Stop(Process serve)
    {
        if (!serve.HasExited)
        {
            serve.Kill(entireProcessTree: false);   // this serve only, by its own handle
            _ = serve.WaitForExit(10_000);
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
