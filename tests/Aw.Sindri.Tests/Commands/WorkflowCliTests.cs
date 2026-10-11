using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.ShowWorkflow;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri workflow' arguments and text: options parse strictly, and each view says the state in words.</summary>
public sealed class WorkflowCliTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public void Workflow_arguments_take_positionals_flags_single_and_repeated_options()
    {
        var (args, problem) = WorkflowArgs.Parse(["signal", "wf-0123456789ab", "passed", "--set", "a=1", "--set", "b=2", "--tokens", "40", "--json"]);

        Assert.Null(problem);
        Assert.Equal(["signal", "wf-0123456789ab", "passed"], args!.Positionals);
        Assert.Equal(["a=1", "b=2"], args.All("--set"));
        Assert.Equal(40, args.Number("--tokens"));
        Assert.True(args.Json);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--colour", "unknown option --colour")]
    [InlineData("--reason", "--reason needs a value")]
    [InlineData("--reason a --reason b", "--reason is given twice")]
    public void A_bad_option_is_a_problem_not_a_guess(string line, string expected) =>
        Assert.Equal(expected, WorkflowArgs.Parse(line.Split(' ')).Problem);

    [Theory, Trait("Lane", "unit")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("lots")]
    public void A_numeric_option_that_is_not_a_whole_number_is_flagged(string value) =>
        Assert.True(WorkflowArgs.Parse(["--tokens", value]).Args!.BadNumber("--tokens"));

    [Fact, Trait("Lane", "unit")]
    public void An_empty_list_says_so() =>
        Assert.Equal("no workflow instances\n", WorkflowText.List([]));

    [Fact, Trait("Lane", "unit")]
    public async Task An_outcome_line_names_the_state_status_and_version_and_a_park_says_why()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var parked = (await h.Engine.Apply(i.Id, new ParkCommand(ParkReason.NeedsAnswer, "which API?"), Token)).Value;
        var text = WorkflowText.Outcome(parked);

        Assert.Contains($"{i.Id.Value}  workflow.loop  ITEM-1#1  work", text, StringComparison.Ordinal);
        Assert.Contains("v2", text, StringComparison.Ordinal);
        Assert.Contains(": which API?", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Show_lists_the_rows_a_human_reads_in_order()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var shown = (await new ShowWorkflowService(h.Store, h.Catalog).Handle(new ShowWorkflowRequest(i.Id), Token)).Value;

        var rows = WorkflowText.Show(shown).Split('\n').Select(l => l.Split(' ')[0]).Where(w => w.Length > 0).ToList();

        Assert.Equal(["instance", "recipe", "item", "state", "status", "timer", "loops", "spent", "facts", "evidence", "signals", "next", "history"], rows.Take(13), StringComparer.Ordinal);
        Assert.Contains("signals   passed, failed", WorkflowText.Show(shown), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_paused_timer_says_the_time_left_in_whole_units()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        h.Clock.Advance(TimeSpan.FromSeconds(75.4));
        _ = (await h.Engine.Apply(i.Id, new TakeOverCommand(), Token)).Value;
        var shown = (await new ShowWorkflowService(h.Store, h.Catalog).Handle(new ShowWorkflowRequest(i.Id), Token)).Value;

        Assert.Contains("timer     paused, 8m 44s left\n", WorkflowText.Show(shown), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_block_line_gives_its_kind_ports_and_flags()
    {
        await using var h = new WorkflowHarness();

        var text = BlockText.Render([h.Blocks.Find(BlockId.From("gate.verifier.passed"))!]);

        Assert.StartsWith("gate.verifier.passed", text, StringComparison.Ordinal);
        Assert.Contains("gate ", text, StringComparison.Ordinal);
        Assert.Contains("rung judge", text, StringComparison.Ordinal);
    }
}
