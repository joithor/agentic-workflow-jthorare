using Aw.Kernel;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>Checks an evidence request's inputs against the provider's declaration: every declared input present and of its type, nothing else.</summary>
internal static class ProviderInputs
{
    internal static Error? Problem(BlockSpec spec, IReadOnlyDictionary<string, string> inputs)
    {
        var problems = spec.Inputs.Where(i => !inputs.TryGetValue(i.Name, out var v) || !i.Accepts(v)).Select(i => $"needs {i}")
            .Concat(inputs.Keys.Where(k => spec.Inputs.All(i => i.Name != k)).Order(StringComparer.Ordinal).Select(k => $"doesn't take '{k}'"))
            .ToList();
        return problems.Count == 0
            ? null
            : SindriErrors.ProviderInputInvalid.With($"{spec.Id.Value} {string.Join("; ", problems)} (inputs: {string.Join(", ", spec.Inputs)})");
    }
}
