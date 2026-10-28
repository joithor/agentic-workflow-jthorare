using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>What the preparer makes before bubblewrap starts, and what it leaves to the engine.</summary>
[Lane(TestLane.Integration)]
public sealed class RunAreaPreparerTests
{
    [Fact]
    public void A_missing_worktree_is_015_and_is_never_created()
    {
        using var area = new TempRunArea();
        var plan = area.Plan with { Worktree = area.Root + "/no-such-worktree" };

        var refusal = RunAreaPreparer.Prepare(plan);

        Assert.Equal("SND-LAUNCH-015", refusal?.Code);
        Assert.Equal("worktree-outside-repo", SandboxRefusal.ReasonOf(refusal!));
        Assert.False(Directory.Exists(area.Root + "/no-such-worktree"));
    }

    [Fact]
    public void A_missing_run_directory_is_016_and_is_never_created()
    {
        using var area = new TempRunArea();
        var plan = area.Plan with { RunDirectory = area.Root + "/no-such-run" };

        var refusal = RunAreaPreparer.Prepare(plan);

        Assert.Equal("SND-LAUNCH-016", refusal?.Code);
        Assert.False(Directory.Exists(area.Root + "/no-such-run"));
    }

    [Fact]
    public void The_read_write_state_the_hooks_share_is_made_when_it_is_missing_but_the_engines_directories_are_not()
    {
        using var area = new TempRunArea();
        var plan = area.Plan with
        {
            Worktree = area.Root + "/run",
            Mounts =
            [
                new PlanMount(area.Root + "/state/locks", PlanMountKind.ReadWrite),
                new PlanMount(area.Root + "/state/judge", PlanMountKind.ReadWrite),
                new PlanMount(area.Root + "/state/cache", PlanMountKind.ReadWrite, Optional: true),
                new PlanMount(area.Root + "/run", PlanMountKind.ReadWrite),
            ],
        };

        var refusal = RunAreaPreparer.Prepare(plan);

        Assert.Null(refusal);
        Assert.True(Directory.Exists(area.Root + "/state/locks") && Directory.Exists(area.Root + "/state/judge"));
        Assert.False(Directory.Exists(area.Root + "/state/cache"));
    }

    [Fact]
    public async Task The_run_area_gets_a_refusing_pre_push_an_empty_global_config_and_the_plan_file_that_a_second_prepare_rewrites_the_same()
    {
        using var area = new TempRunArea();

        Assert.Null(RunAreaPreparer.Prepare(area.Plan));
        var first = await File.ReadAllTextAsync(area.Root + "/area/sandbox-plan.json", TestContext.Current.CancellationToken);
        Assert.Null(RunAreaPreparer.Prepare(area.Plan));

        var hook = await File.ReadAllTextAsync(area.Root + "/area/githooks/pre-push", TestContext.Current.CancellationToken);
        Assert.Contains("exit 1", hook, StringComparison.Ordinal);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(area.Root + "/area/githooks/pre-push"));
        Assert.Equal(string.Empty, await File.ReadAllTextAsync(area.Root + "/area/gitconfig.empty", TestContext.Current.CancellationToken));
        Assert.Equal(first, await File.ReadAllTextAsync(area.Root + "/area/sandbox-plan.json", TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(area.Root + "/area/shims"));
    }

    [Fact]
    public async Task The_plan_file_is_the_real_plan_that_sandbox_explain_reads_back()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET");

        Assert.Null(RunAreaPreparer.Prepare(area.Plan));

        var bytes = await File.ReadAllBytesAsync(area.Root + "/area/sandbox-plan.json", TestContext.Current.CancellationToken);
        var read = AwJson.Deserialize<SandboxPlan>(bytes, AwJson.CreateOptions(SindriJsonContext.Default));
        Assert.True(read.IsSuccess);
        Assert.Equal(area.Plan.PlanHash, read.Value.PlanHash);
        Assert.Equal(area.Plan.Launch, read.Value.Launch);
        Assert.Equal(area.Plan.Mounts.Count, read.Value.Mounts.Count);
        Assert.Null(read.Value.Environment.First(e => e.Name == "AW_SINDRI_LAUNCH_SECRET").Value);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(area.Root + "/area/sandbox-plan.json"));
    }

    [Fact]
    public void A_plan_built_for_the_fixture_box_names_the_run_area_through_the_plan_file_variable() =>
        Assert.Equal(Req.RunArea, RunAreaPreparer.RunAreaOf(SandboxPlannerTests.Build(Req.WriteScope)));
}
