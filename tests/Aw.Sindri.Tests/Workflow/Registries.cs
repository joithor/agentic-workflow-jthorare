using System.Text;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Builtin;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Infrastructure.Recipes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The built-in block registry (with the tool blocks over fakes) and the built-in recipe files, for tests.</summary>
internal static class Registries
{
    internal static BlockRegistry Builtin(params IBlock[] extra) => new(
        [.. BuiltinSteps.All, .. BuiltinGates.All, new NotifyHumanEffect(SliceHarness.Json), new DraftPullRequestEffect(SliceHarness.Json), .. Tools(), .. extra]);

    /// <summary>The tool blocks AddSindriToolBlocks registers, over the fakes.</summary>
    internal static IReadOnlyList<IBlock> Tools(IJudgeClient? judge = null, IUiEvidence? ui = null)
    {
        judge ??= new FakeJudgeClient();
        ui ??= new FakeUiEvidence();
        return [.. JudgeQuestions.All.Select(q => new JudgeGate(q, judge)), new StackDoctorGate(ui), new ProvenanceGate(ui), UiEvidenceSteps.UiRun,
            UiRunProvider.UiBrowser(ui), UiRunProvider.DesignParity(ui)];
    }

    internal static string BuiltinYaml(string name)
    {
        using var stream = typeof(FileRecipeCatalog).Assembly.GetManifestResourceStream($"Aw.Sindri.Infrastructure.Recipes.Builtin.{name}.yaml")
            ?? throw new InvalidOperationException($"no built-in recipe {name}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
