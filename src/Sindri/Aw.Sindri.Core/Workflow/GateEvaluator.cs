using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Evaluates a transition's gates through the block registry over the instance's facts with the signal's outputs merged
/// in, as a ladder (§17.28.1): cheapest rung first (deterministic, judge, model; recipe order within a rung), stopping at
/// the first failure, so a model gate runs only when every cheaper gate passed. The records list the gates that ran.
/// </summary>
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

        var context = new GateContext(instance.Id, instance.Recipe, instance.Item, instance.Attempt, instance.State, transition.To, transition.On, facts, instance.Evidence,
            signal.Sha);
        var ladder = new List<(BlockId Id, IGateBlock Gate)>();
        foreach (var id in transition.Gates ?? [])
        {
            if (blocks.Gate(id) is not { } found)
            {
                return SindriErrors.RecipeInvalid.With($"gate '{id.Value}' isn't registered");
            }

            ladder.Add((id, found));
        }

        var records = new List<GateRecord>();
        foreach (var (id, gate) in ladder.OrderBy(g => g.Gate.Spec.Rung ?? GateRung.Deterministic))
        {
            var verdict = await gate.Evaluate(context, ct).ConfigureAwait(false);
            if (verdict.IsError)
            {
                return verdict.Error;
            }

            var v = verdict.Value;
            records.Add(new GateRecord(id, gate.Spec.Rung ?? GateRung.Deterministic, v.Passed, v.Detail, v.Score, v.Threshold));
            if (!v.Passed)
            {
                break;
            }
        }

        return records;
    }
}
