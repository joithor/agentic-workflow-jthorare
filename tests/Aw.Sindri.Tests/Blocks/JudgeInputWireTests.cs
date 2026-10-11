using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Judge;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>
/// The judge reads each question's input by field name (docs/csharp/judge.md on #156, Questions). These pin the exact JSON
/// each record writes through the package's AwJson options, so a renamed property can't change what judge reads.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class JudgeInputWireTests
{
    private static readonly Dictionary<string, (JudgeInput Input, string Json)> _wire = new(StringComparer.Ordinal)
    {
        ["resolution-check"] = (new ResolutionCheckInput("b", "e", "a", "r", "test", "s", BeforePassed: false, AfterPassed: true, "1 file"),
            """{"brief":"b","expected":"e","actual":"a","rootCause":"r","checkKind":"test","checkSummary":"s","beforePassed":false,"afterPassed":true,"diffStat":"1 file"}"""),
        ["rule-check"] = (new RuleCheckInput("r", "h"), """{"rule":"r","hunk":"h"}"""),
        ["turn-progress"] = (new TurnProgressInput("p", "d"), """{"problem":"p","turnDiff":"d"}"""),
        ["brief-scope"] = (new BriefScopeInput("Explore", "g", "a", "p"), """{"agentType":"Explore","goal":"g","acceptanceCriteria":"a","proofCommand":"p"}"""),
        ["visual-critique"] = (new VisualCritiqueInput("1.png", "/runs/r1"), """{"afterScreenshot":"1.png","evidenceDir":"/runs/r1"}"""),
        ["visual-critique with a baseline"] = (new VisualCritiqueInput("1.png", "/runs/r1", "base.png"), """{"afterScreenshot":"1.png","evidenceDir":"/runs/r1","baselineScreenshot":"base.png"}"""),
        ["ask-check"] = (new AskCheckInput("t"), """{"transcriptTail":"t"}"""),
    };

    public static TheoryData<string> Cases => [.. _wire.Keys];

    [Theory, MemberData(nameof(Cases))]
    public void Each_question_writes_the_field_names_judge_reads(string name)
    {
        var (input, json) = _wire[name];

        Assert.Equal(json, Encoding.UTF8.GetString(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(input, SliceHarness.Json.GetTypeInfo(input.GetType()))));
    }

    [Fact]
    public void Every_gating_question_record_is_in_the_package_json_context()
    {
        foreach (var question in JudgeQuestions.All)
        {
            var record = question.Build(question.Inputs.ToDictionary(i => i.Name, i => i.Type is PortType.Flag ? "true" : "x", StringComparer.Ordinal));

            Assert.Equal(question.Name, record.Question);
            Assert.NotNull(SliceHarness.Json.GetTypeInfo(record.GetType()));
        }
    }
}
