using System.Text;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Builtin;
using Aw.Sindri.Infrastructure.Recipes;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The built-in block registry and the built-in recipe files, for tests.</summary>
internal static class Registries
{
    internal static BlockRegistry Builtin(params IBlock[] extra) => new(
        [.. BuiltinSteps.All, .. BuiltinGates.All, new NotifyHumanEffect(SliceHarness.Json), new DraftPullRequestEffect(SliceHarness.Json), .. extra]);

    internal static string BuiltinYaml(string name)
    {
        using var stream = typeof(FileRecipeCatalog).Assembly.GetManifestResourceStream($"Aw.Sindri.Infrastructure.Recipes.Builtin.{name}.yaml")
            ?? throw new InvalidOperationException($"no built-in recipe {name}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
