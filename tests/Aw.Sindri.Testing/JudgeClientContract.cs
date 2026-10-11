using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Core.Ids;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IJudgeClient"/> honours (the judge CLI contract, docs/csharp/judge.md), run against the fake and the process adapter.</summary>
public abstract class JudgeClientContract
{
    /// <summary>A client whose judge decides <paramref name="answer"/> for its question.</summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The decided or escalated answer.</param>
    /// <returns>The client.</returns>
    protected abstract IJudgeClient Answering(string question, JudgeAnswer answer);

    /// <summary>A client whose judge fails with <paramref name="message"/> (its exit-1 error line).</summary>
    /// <param name="question">The question.</param>
    /// <param name="message">The judge's error message.</param>
    /// <returns>The client.</returns>
    protected abstract IJudgeClient Failing(string question, string message);

    /// <summary>The question and the input record the last call sent, as the judge read them.</summary>
    /// <returns>The question and its input.</returns>
    protected abstract (string Question, JudgeInput Input) Received();

    /// <summary>A decided answer comes back with its decision, confidence, provider and decision id.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_decided_answer_comes_back_whole()
    {
        var expected = new JudgeAnswer("model-decided", "looks-right", 0.9, "claude-cli", JudgeDecisionId.From("d-1"));
        var client = Answering("visual-critique", expected);

        var answer = await client.Ask(new VisualCritiqueInput("shot.png", "/evidence"), TestContext.Current.CancellationToken);

        Expect.Equal(expected, answer.Value);
    }

    /// <summary>An escalation comes back as escalated, with its reason and no decision.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_escalation_comes_back_escalated()
    {
        var client = Answering("ask-check", new JudgeAnswer("below-threshold", Escalated: true));

        var answer = await client.Ask(new AskCheckInput("merge it?"), TestContext.Current.CancellationToken);

        Expect.Equal(new JudgeAnswer("below-threshold", Escalated: true), answer.Value);
    }

    /// <summary>A judge error is SND-BLK-001 and names the judge's message.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_judge_error_is_tool_unavailable_with_its_message()
    {
        var client = Failing("rule-check", "store unavailable: disk full");

        var answer = await client.Ask(new RuleCheckInput("no TODOs", "+ // TODO"), TestContext.Current.CancellationToken);

        Expect.True(answer.IsError && answer.Error.Code == SindriErrors.ToolUnavailable.Code && answer.Error.Message.Contains("disk full", StringComparison.Ordinal));
    }

    /// <summary>The judge receives the input's question and the whole input record.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_judge_receives_the_question_and_the_input_record()
    {
        var client = Answering("resolution-check", new JudgeAnswer("pre-rule", "unresolved", 1, "rules", JudgeDecisionId.From("d-2")));
        var input = new ResolutionCheckInput("login fails", "login works", "500", "null user", "test", "login test", BeforePassed: false, AfterPassed: true, "1 file", "diff text");

        _ = await client.Ask(input, TestContext.Current.CancellationToken);

        Expect.Equal(("resolution-check", input), Received());
    }
}
