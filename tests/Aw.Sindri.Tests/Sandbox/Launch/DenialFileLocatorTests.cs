using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>Where the denial collector finds a launch's transcript and fail-open log, from its plan.</summary>
[Lane(TestLane.Unit)]
public sealed class DenialFileLocatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static SandboxPlan PlanWith(params PlanEnvVar[] extra)
    {
        var plan = SandboxPlanner.Build(Aw.Sindri.Core.Sandbox.SandboxMechanism.Bubblewrap, SandboxRequests.WriteScope).Value;
        return plan with { Environment = [.. plan.Environment, .. extra] };
    }

    [Fact]
    public void A_claude_launch_reads_the_newest_jsonl_under_its_config_projects()
    {
        var dir = Path.Combine(_temp.Path, "projects", "x");
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "old.jsonl"), "{}");
        File.SetLastWriteTimeUtc(Path.Combine(dir, "old.jsonl"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.WriteAllText(Path.Combine(dir, "new.jsonl"), "{}");

        var found = DenialFileLocator.For(PlanWith(new PlanEnvVar("CLAUDE_CONFIG_DIR", _temp.Path, "login")));

        Expect.Equal(TranscriptKind.Claude, found.Kind);
        Expect.Equal(Path.Combine(dir, "new.jsonl"), found.Transcript);
    }

    [Fact]
    public void A_codex_launch_reads_under_its_sessions()
    {
        var found = DenialFileLocator.For(PlanWith(new PlanEnvVar("CODEX_HOME", _temp.Path, "login")));

        Expect.Equal(TranscriptKind.Codex, found.Kind);
        Expect.True(found.Transcript is null);   // no sessions folder yet
    }
}
