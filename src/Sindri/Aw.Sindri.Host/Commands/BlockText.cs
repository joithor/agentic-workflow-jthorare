using Aw.Sindri.Core.Blocks;

namespace Aw.Sindri.Host.Commands;

/// <summary>The block registry as text: one line per block, its kind, typed inputs and outputs, and its flags.</summary>
internal static class BlockText
{
    internal static string Render(IReadOnlyList<BlockSpec> blocks) => string.Join('\n', blocks.Select(Line)) + "\n";

    private static string Line(BlockSpec b)
    {
        var flags = new List<string>();
        if (b.Capability)
        {
            flags.Add("capability");
        }

        if (b.Rung is { } rung)
        {
            flags.Add("rung " + rung.ToString().ToLowerInvariant());
        }

        if (b.CommandKind is { } kind)
        {
            flags.Add("command " + kind);
        }

        return $"{b.Id.Value,-24} {b.Kind.ToString().ToLowerInvariant(),-8} in [{string.Join(", ", b.Inputs)}] out [{string.Join(", ", b.Outputs)}]"
            + (flags.Count == 0 ? string.Empty : $"  {string.Join(", ", flags)}");
    }
}
