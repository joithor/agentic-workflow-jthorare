using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>A phase workspace's base commit for the host-side prepare is the commit it was seeded at (sandbox spec §3.8), never the HEAD of the worktree an earlier Step's agent has been committing in.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseSeedTests
{
    private const string Seed = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void A_phase_workspace_launch_uses_the_seed_commit_not_HEAD()
    {
        Expect.Equal(Seed, PhaseSeed.BaseOf($$"""{"line":"l1","phase":"p1","base":"{{Seed}}"}"""));
        Expect.Equal(null, PhaseSeed.BaseOf("""{"line":"l1","phase":"p1","base":"HEAD"}"""));   // not a full sha: no base, and the prepare is skipped
        Expect.Equal(null, PhaseSeed.BaseOf("not json"));
        Expect.Equal(null, PhaseSeed.BaseOf("""{"line":"l1"}"""));
    }
}
