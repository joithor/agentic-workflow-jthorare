using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Blocks;

[Lane(TestLane.Unit)]
public sealed class FakeJudgeClientContractTests : JudgeClientContract
{
    private FakeJudgeClient _judge = new();

    protected override IJudgeClient Answering(string question, JudgeAnswer answer) => _judge = new FakeJudgeClient().Answer(question, answer);

    protected override IJudgeClient Failing(string question, string message) =>
        _judge = new FakeJudgeClient().Answer(question, SindriErrors.ToolUnavailable.With($"judge {question} exited 1: {message}"));

    protected override (string Question, JudgeInput Input) Received() => (_judge.Asked[^1].Question, _judge.Asked[^1]);
}
