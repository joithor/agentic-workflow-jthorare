using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>Each judge decision question as a rung-2 gate: what passes, what refuses, and what reaches the judge.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeGateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, string, string> Decisions => new()
    {
        { "resolution-check", "resolved", "partial" },
        { "rule-check", "fine", "violated" },
        { "rule-check", "n/a", "violated" },
        { "turn-progress", "progressing", "stalled" },
        { "brief-scope", "ready", "missing" },
        { "visual-critique", "looks-right", "sloppy" },
        { "ask-check", "continue", "ask" },
    };

    [Fact]
    public void Each_gating_question_is_a_capability_judge_gate_named_for_it()
    {
        var specs = JudgeQuestions.All.Select(q => new JudgeGate(q, new FakeJudgeClient()).Spec).ToList();

        Assert.Equal(["gate.judge.resolution-check", "gate.judge.rule-check", "gate.judge.turn-progress", "gate.judge.brief-scope",
            "gate.judge.visual-critique", "gate.judge.ask-check"], specs.Select(s => s.Id.Value), StringComparer.Ordinal);
        Assert.All(specs, s => Assert.True(s is { Kind: BlockKind.Gate, Rung: GateRung.Judge, Capability: true } && s.Inputs.Count > 0));
    }

    [Theory, MemberData(nameof(Decisions))]
    public async Task A_gate_passes_only_on_its_passing_decisions(string question, string pass, string fail)
    {
        var passed = await Evaluate(question, new JudgeAnswer("model-decided", pass, 0.91, "claude-cli", JudgeDecisionId.From("d-1")));
        var refused = await Evaluate(question, new JudgeAnswer("model-decided", fail, 0.88, "claude-cli", JudgeDecisionId.From("d-2")));

        Assert.True(passed.Value.Passed);
        Assert.Equal(0.91, passed.Value.Score);
        Assert.Contains("decision d-1", passed.Value.Detail, StringComparison.Ordinal);
        Assert.False(refused.Value.Passed);
        Assert.Contains($"{fail} by claude-cli", refused.Value.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_escalation_never_passes_and_names_its_reason()
    {
        var verdict = await Evaluate("visual-critique", new JudgeAnswer("no-provider-decided", Escalated: true));

        Assert.False(verdict.Value.Passed);
        Assert.Equal("judge visual-critique: escalated (no-provider-decided)", verdict.Value.Detail);
    }

    [Fact]
    public async Task A_judge_that_cant_be_run_is_an_error()
    {
        var verdict = await Evaluate("visual-critique", SindriErrors.ToolUnavailable.With("judge can't be started"));

        Assert.Equal(SindriErrors.ToolUnavailable.Code, verdict.Error.Code);
    }

    [Fact]
    public async Task A_missing_or_mistyped_fact_refuses_without_asking_the_judge()
    {
        var judge = new FakeJudgeClient();
        var gate = new JudgeGate(JudgeQuestions.ResolutionCheck, judge);
        var facts = FactsFor(JudgeQuestions.ResolutionCheck);
        facts["resolution.after-passed"] = "yes";
        _ = facts.Remove("resolution.brief");

        var verdict = await gate.Evaluate(Context(facts), Token);

        Assert.False(verdict.Value.Passed);
        Assert.Equal("judge resolution-check: needs resolution.brief:text, resolution.after-passed:flag", verdict.Value.Detail);
        Assert.Empty(judge.Asked);
    }

    [Fact]
    public async Task The_input_record_is_built_from_the_facts()
    {
        var judge = new FakeJudgeClient();
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["visual.screenshot"] = "desktop/1.png",
            ["visual.evidence-dir"] = "/runs/r1",
            ["visual.baseline"] = "base.png",
        };

        _ = await new JudgeGate(JudgeQuestions.VisualCritique, judge).Evaluate(Context(facts), Token);
        _ = await new JudgeGate(JudgeQuestions.ResolutionCheck, judge).Evaluate(Context(FactsFor(JudgeQuestions.ResolutionCheck)), Token);

        Assert.Equal(new VisualCritiqueInput("desktop/1.png", "/runs/r1", "base.png"), judge.Asked[0]);
        var resolution = Assert.IsType<ResolutionCheckInput>(judge.Asked[1]);
        Assert.True(resolution is { BeforePassed: false, AfterPassed: true, Diff: null });
    }

    private static async Task<Result<GateVerdict>> Evaluate(string question, Result<JudgeAnswer> answer)
    {
        var q = JudgeQuestions.All.Single(x => x.Name == question);
        return await new JudgeGate(q, new FakeJudgeClient().Answer(question, answer)).Evaluate(Context(FactsFor(q)), Token);
    }

    private static Dictionary<string, string> FactsFor(JudgeQuestion question) =>
        question.Inputs.ToDictionary(i => i.Name, i => i.Type is PortType.Flag ? (i.Name.EndsWith("before-passed", StringComparison.Ordinal) ? "false" : "true") : "some text", StringComparer.Ordinal);

    private static GateContext Context(IReadOnlyDictionary<string, string> facts) =>
        new(InstanceId.From("wf-000000000001"), RecipeId.From("workflow.ui-evidence"), ItemId.From("ITEM-1"), 1, "run", "done", "ran", facts, [], Sha: null);
}
