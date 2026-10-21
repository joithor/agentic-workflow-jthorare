using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Features.Pr;

namespace Aw.Sindri.Tests.Pr;

/// <summary>The snapshot's outcome priority (PR babysitter spec §4.3, the lite slice): merged, closed, conflict, ciRed, the rest of attention, pending, green; checks count only on the head.</summary>
[Lane(TestLane.Unit)]
public sealed class PrSnapshotRulesTests
{
    private const string Old = "0ld0ld00";

    [Fact]
    public void A_merged_pull_request_is_merged_whatever_its_checks_say() =>
        Assert.Equal("merged", PrSnapshotRules.Outcome(PrContexts.Pull() with { State = PullState.Merged }));

    [Fact]
    public void A_closed_pull_request_is_closed_even_when_red() =>
        Assert.Equal("closed", PrSnapshotRules.Outcome(PrContexts.Pull() with { State = PullState.Closed }));

    [Fact]
    public void A_conflict_is_attention_even_on_a_red_head() =>
        Assert.Equal("attention", PrSnapshotRules.Outcome(PrContexts.Pull(mergeable: false)));

    [Fact]
    public void A_red_head_is_ciRed() =>
        Assert.Equal("ciRed", PrSnapshotRules.Outcome(PrContexts.Pull()));

    [Fact]
    public void A_red_head_with_an_open_thread_is_ciRed_first() =>
        Assert.Equal("ciRed", PrSnapshotRules.Outcome(PrContexts.Pull(threads: 2)));

    [Fact]
    public void A_red_head_with_changes_requested_is_ciRed_first() =>
        Assert.Equal("ciRed", PrSnapshotRules.Outcome(PrContexts.Pull(review: "CHANGES_REQUESTED")));

    [Fact]
    public void A_green_head_with_an_open_thread_is_attention() =>
        Assert.Equal("attention", PrSnapshotRules.Outcome(PrContexts.Pull(PullChecksState.Passing, threads: 1)));

    [Fact]
    public void A_green_head_with_changes_requested_is_attention() =>
        Assert.Equal("attention", PrSnapshotRules.Outcome(PrContexts.Pull(PullChecksState.Passing, review: "CHANGES_REQUESTED")));

    [Theory]
    [InlineData(PullChecksState.Pending)]
    [InlineData(PullChecksState.None)]
    public void Checks_still_running_or_not_started_are_pending(PullChecksState state) =>
        Assert.Equal("pending", PrSnapshotRules.Outcome(PrContexts.Pull(state)));

    [Fact]
    public void Checks_that_passed_on_the_head_are_green() =>
        Assert.Equal("green", PrSnapshotRules.Outcome(PrContexts.Pull(PullChecksState.Passing)));

    [Fact]
    public void A_mergeable_still_being_computed_does_not_hold_green_back() =>
        Assert.Equal("green", PrSnapshotRules.Outcome(PrContexts.Pull(PullChecksState.Passing, mergeable: null)));

    [Theory]
    [InlineData(PullChecksState.Failing)]
    [InlineData(PullChecksState.Passing)]
    public void Checks_on_an_older_head_are_pending_never_ciRed_or_green(PullChecksState state) =>
        Assert.Equal("pending", PrSnapshotRules.Outcome(PrContexts.Pull(state, checksSha: Old)));

    [Fact]
    public void Checks_on_an_older_head_with_an_open_thread_are_attention() =>
        Assert.Equal("attention", PrSnapshotRules.Outcome(PrContexts.Pull(PullChecksState.Failing, checksSha: Old, threads: 1)));

    [Fact]
    public void The_head_and_the_checks_sha_compare_without_case() =>
        Assert.Equal("green", PrSnapshotRules.Outcome(PrContexts.Pull(PullChecksState.Passing, checksSha: PrContexts.Head.ToUpperInvariant())));
}
