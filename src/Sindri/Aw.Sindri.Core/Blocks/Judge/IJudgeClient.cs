using Aw.Kernel;

namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>Asks the judge one decision question (its CLI contract, docs/csharp/judge.md: 'judge &lt;question&gt; &lt; input.json').</summary>
public interface IJudgeClient
{
    /// <summary>Asks <paramref name="input"/>'s question once; the judge records one decision for every call.</summary>
    /// <param name="input">The question's input record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The decided or escalated answer, or SND-BLK-001 when the judge couldn't be run or answered outside its contract.</returns>
    public ValueTask<Result<JudgeAnswer>> Ask(JudgeInput input, CancellationToken cancellationToken);
}
