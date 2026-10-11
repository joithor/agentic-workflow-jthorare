using Aw.Kernel;

namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>A deterministic gate over the instance's facts: <paramref name="rule"/> decides from the facts its inputs name.</summary>
/// <param name="spec">The declaration (kind gate, with its rung and inputs).</param>
/// <param name="rule">The decision over the facts.</param>
public sealed class FactGate(BlockSpec spec, Func<IReadOnlyDictionary<string, string>, GateVerdict> rule) : IGateBlock
{
    /// <inheritdoc/>
    public BlockSpec Spec { get; } = spec;

    /// <inheritdoc/>
    public ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var missing = Spec.Inputs.Where(i => !context.Facts.ContainsKey(i.Name)).Select(i => i.Name).ToList();
        var verdict = missing.Count > 0
            ? new GateVerdict(Passed: false, $"missing {string.Join(", ", missing)}")
            : rule(context.Facts);
        return ValueTask.FromResult<Result<GateVerdict>>(verdict);
    }
}
