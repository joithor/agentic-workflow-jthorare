using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>
/// A judge question as a gate (rung 2, <see cref="GateRung.Judge"/>). It bears a capability: each evaluation runs the judge,
/// which may start an agent CLI on a model, so only approved recipes may name it. Escalation never passes: an unsure judge
/// refuses the transition. A judge that can't be run is an error, which refuses it too.
/// </summary>
/// <param name="question">The question.</param>
/// <param name="judge">The judge.</param>
internal sealed class JudgeGate(JudgeQuestion question, IJudgeClient judge) : IGateBlock
{
    /// <inheritdoc/>
    public BlockSpec Spec { get; } = new(BlockId.From($"gate.judge.{question.Name}"), BlockKind.Gate, question.Summary, question.Inputs, [],
        Capability: true, Rung: GateRung.Judge);

    /// <inheritdoc/>
    public async ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var missing = Spec.Inputs.Where(i => !context.Facts.TryGetValue(i.Name, out var v) || !i.Accepts(v)).Select(i => i.ToString()).ToList();
        if (missing.Count > 0)
        {
            return new GateVerdict(Passed: false, $"judge {question.Name}: needs {string.Join(", ", missing)}");
        }

        var answer = await judge.Ask(question.Build(context.Facts), cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : Verdict(answer.Value);
    }

    private GateVerdict Verdict(JudgeAnswer answer)
    {
        var decision = answer.Id is { } id ? $", decision {id.Value}" : string.Empty;
        if (answer.Escalated || answer.Decision is null)
        {
            return new GateVerdict(Passed: false, $"judge {question.Name}: escalated ({answer.ReasonCode}){decision}", answer.Confidence);
        }

        var passed = question.Passing.Contains(answer.Decision, StringComparer.Ordinal);
        return new GateVerdict(passed, $"judge {question.Name}: {answer.Decision} by {answer.Model ?? "?"} ({answer.ReasonCode}){decision}", answer.Confidence);
    }
}
