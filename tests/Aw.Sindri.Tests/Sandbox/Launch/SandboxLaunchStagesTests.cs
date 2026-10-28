using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The sandbox step's launch parts: the plan request from the launch, the login and the learn history; the egress and its release.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxLaunchStagesTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly StubInputs _inputs = new();
    private readonly StubEgress _egress = new();
    private readonly FakeModelCredentialSource _login = new FakeModelCredentialSource(ModelLoginSource.OwnerCopy).With(AgentId.From("claude-code"), LoginState.Ready);

    private SandboxLaunchStages Make(params IModelCredentialSource[] logins) =>
        new(new NoProfile(), new EmptyLearn(), logins.Length == 0 ? [_login] : logins, _inputs, _egress, _clock);

    private static LaunchRequest Step() =>
        A.LaunchRequest().InDirectory("/work/web").Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).Build();

    [Fact]
    public async Task Request_names_the_launch_paths_net_host_login_and_learn_history()
    {
        var request = Step();

        var built = await Make().Request(request, CancellationToken.None);

        Expect.True(built.IsSuccess);
        var r = built.Value;
        Expect.Equal(request.Launch, r.Launch);
        Expect.Equal("/run/area/" + request.Launch.Value, r.Paths.RunArea);
        Expect.Equal("/net/" + request.Launch.Value, r.Net.NetDir);
        Expect.Equal(7480, r.Net.SindriPort);
        Expect.True(!r.Attended && r.Login is { Source: ModelLoginSource.OwnerCopy });
        Expect.True(r.Learn is { } learn && learn.Now == _clock.GetUtcNow());
        Expect.Equal("web", r.Repo.Value);   // no profile repo: the directory's name
    }

    [Fact]
    public async Task Request_refuses_with_the_login_code_when_no_source_supports_the_agent()
    {
        var built = await Make(new FakeModelCredentialSource(ModelLoginSource.OwnerCopy)).Request(Step(), CancellationToken.None);

        Expect.True(built.IsError && string.Equals(built.Error.Code, SindriErrors.LaunchLoginUnavailable.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartEgress_hands_the_plan_network_to_the_egress_and_Release_stops_it()
    {
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, (await Make().Request(Step(), CancellationToken.None)).Value).Value;
        var stages = Make();

        var started = await stages.StartEgress(plan, CancellationToken.None);
        await stages.Release(plan.Launch, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.True(ReferenceEquals(plan.Network, _egress.Started[plan.Launch]));
        Expect.True(_egress.Released.Contains(plan.Launch));
    }

    private sealed class NoProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LoadedProfile?>(null));
    }

    private sealed class EmptyLearn : ILearnHistory
    {
        public ValueTask<LearnHistory> Read(string repo, CancellationToken cancellationToken) => ValueTask.FromResult(new LearnHistory(null, 0, null));
    }

    private sealed class StubInputs : ISandboxLaunchInputs
    {
        public SandboxHostFacts Host() => SandboxRequests.Host;

        public ValueTask<string> GitCommonDir(string repoPath, CancellationToken cancellationToken) => ValueTask.FromResult(repoPath + "/.git");

        public string RunAreaFor(LaunchId launch) => "/run/area/" + launch.Value;

        public string NetDirFor(LaunchId launch) => "/net/" + launch.Value;

        public int SindriPort() => 7480;

        public int ProxyPort() => 3128;
    }

    private sealed class StubEgress : ILaunchEgress
    {
        public Dictionary<LaunchId, PlanNetwork> Started { get; } = [];

        public List<LaunchId> Released { get; } = [];

        public ValueTask<Result<Unit>> Start(LaunchId launch, PlanNetwork network, CancellationToken cancellationToken)
        {
            Started[launch] = network;
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        public ValueTask Release(LaunchId launch)
        {
            Released.Add(launch);
            return ValueTask.CompletedTask;
        }
    }
}
