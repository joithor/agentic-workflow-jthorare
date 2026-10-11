using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Infrastructure.Tools;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>The process adapter against a scripted judge that speaks the judge CLI contract (exit 0 decided, 2 escalated, 1 error, 64 usage).</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessJudgeClientContractTests : JudgeClientContract, IDisposable
{
    private readonly StubTool _tool = new("printf '%s' \"$1\" > \"$d/question\"\ncat > \"$d/stdin\"\ncat \"$d/stdout\"\ncat \"$d/stderr\" >&2\nexit $(cat \"$d/exit\")\n");

    public void Dispose() => _tool.Dispose();

    [Fact]
    public async Task A_missing_binary_is_tool_unavailable()
    {
        var client = new ProcessJudgeClient(new ToolSettings(Path.Combine(_tool.Directory, "no-such-judge"), "ui-evidence"), SliceHarness.Json);

        var answer = await client.Ask(new AskCheckInput("x"), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ToolUnavailable.Code, answer.Error.Code);
        Assert.Contains("can't be started", answer.Error.Message, StringComparison.Ordinal);
    }

    [Theory, InlineData(64, ""), InlineData(0, "not json"), InlineData(0, "{\"reasonCode\":\"x\"}")]
    public async Task An_answer_outside_the_contract_is_tool_unavailable(int exit, string stdout)
    {
        var client = Script(exit, stdout, exit == 64 ? "usage: judge <command> [args]" : string.Empty);

        var answer = await client.Ask(new AskCheckInput("x"), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ToolUnavailable.Code, answer.Error.Code);
    }

    [Fact]
    public async Task Ask_check_continue_on_exit_2_reads_as_a_decision()
    {
        var client = Script(2, "{\"decision\":\"continue\",\"confidence\":0.8,\"model\":\"claude-cli\",\"reasonCode\":\"model-decided\",\"id\":\"d-9\"}", string.Empty);

        var answer = await client.Ask(new AskCheckInput("x"), TestContext.Current.CancellationToken);

        Assert.Equal("continue", answer.Value.Decision);
    }

    protected override IJudgeClient Answering(string question, JudgeAnswer answer) =>
        Script(answer.Escalated ? 2 : 0, Encoding.UTF8.GetString(AwJson.Serialize(answer, SliceHarness.Json)), string.Empty);

    protected override IJudgeClient Failing(string question, string message) => Script(1, string.Empty, message);

    protected override (string Question, JudgeInput Input) Received()
    {
        var question = _tool.Read("question");
        var stdin = Encoding.UTF8.GetBytes(_tool.Read("stdin"));
        JudgeInput input = question switch
        {
            "resolution-check" => AwJson.Deserialize<ResolutionCheckInput>(stdin, SliceHarness.Json).Value,
            "visual-critique" => AwJson.Deserialize<VisualCritiqueInput>(stdin, SliceHarness.Json).Value,
            "rule-check" => AwJson.Deserialize<RuleCheckInput>(stdin, SliceHarness.Json).Value,
            _ => AwJson.Deserialize<AskCheckInput>(stdin, SliceHarness.Json).Value,
        };
        return (question, input);
    }

    private ProcessJudgeClient Script(int exit, string stdout, string stderr)
    {
        _tool.Write("exit", exit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _tool.Write("stdout", stdout);
        _tool.Write("stderr", stderr);
        return new ProcessJudgeClient(new ToolSettings(_tool.Binary, "ui-evidence"), SliceHarness.Json);
    }
}
