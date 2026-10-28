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

    private const string Glob = "~/.claude/projects/*/{sessionId}.jsonl";

    [Fact]
    public void The_launch_s_own_transcript_is_picked_even_when_another_one_is_newer()
    {
        var dir = Path.Combine(_temp.Path, "projects", "x");
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mine.jsonl"), "{}");
        File.SetLastWriteTimeUtc(Path.Combine(dir, "mine.jsonl"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.WriteAllText(Path.Combine(dir, "other.jsonl"), "{}");   // another launch's, written later

        var (transcript, kind, _) = DenialFileLocator.For(PlanWith(new PlanEnvVar("CLAUDE_CONFIG_DIR", _temp.Path, "login")), Glob, "mine");

        Expect.Equal(TranscriptKind.Claude, kind);
        Expect.Equal(Path.Combine(dir, "mine.jsonl"), transcript);
    }

    [Fact]
    public void No_session_or_no_match_skips_the_transcript_and_still_gives_the_fail_open_log()
    {
        var plan = PlanWith(new PlanEnvVar("CLAUDE_CONFIG_DIR", _temp.Path, "login"));

        Expect.True(DenialFileLocator.For(plan, Glob, session: null).Transcript is null);
        Expect.True(DenialFileLocator.For(plan, Glob, "missing").Transcript is null);
        Expect.True(DenialFileLocator.For(plan, glob: null, "mine").Transcript is null);
        Expect.True(DenialFileLocator.For(plan, Glob, session: null).FailOpenLog is { } log && log.EndsWith("failopen.log", StringComparison.Ordinal));
    }

    [Fact]
    public void A_codex_launch_reads_under_its_codex_home()
    {
        var dir = Path.Combine(_temp.Path, "sessions", "2026");
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "rollout-abc.jsonl"), "{}");

        var (transcript, kind, _) = DenialFileLocator.For(PlanWith(new PlanEnvVar("CODEX_HOME", _temp.Path, "login")), "~/.codex/sessions/*/rollout-{sessionId}.jsonl", "abc");

        Expect.Equal(TranscriptKind.Codex, kind);
        Expect.Equal(Path.Combine(dir, "rollout-abc.jsonl"), transcript);
    }
}
