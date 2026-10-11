using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>Evaluates a transition's gates through the block registry, in order, over the instance's facts with the signal's outputs merged in.</summary>
/// <param name="blocks">The block registry.</param>
internal sealed class GateEvaluator(IBlockRegistry blocks)
{
    internal async ValueTask<Result<IReadOnlyList<GateRecord>>> Evaluate(WorkflowInstance instance, RecipeTransition transition, WorkflowSignal signal, CancellationToken ct)
    {
        var facts = new Dictionary<string, string>(instance.Facts.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (name, value) in signal.Outputs)
        {
            facts[name] = value;
        }

        var context = new GateContext(instance.Id, instance.Item, instance.State, facts, instance.Evidence, signal.Sha);
        var records = new List<GateRecord>();
        foreach (var id in transition.Gates ?? [])
        {
            if (blocks.Gate(id) is not { } gate)
            {
                return SindriErrors.RecipeInvalid.With($"gate '{id.Value}' isn't registered");
            }

            var verdict = await gate.Evaluate(context, ct).ConfigureAwait(false);
            if (verdict.IsError)
            {
                return verdict.Error;
            }

            var v = verdict.Value;
            records.Add(new GateRecord(id, gate.Spec.Rung ?? GateRung.Deterministic, v.Passed, v.Detail, v.Score, v.Threshold));
        }

        return records;
    }
}
