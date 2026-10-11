using Aw.Kernel;
using Aw.Sindri.Core.Blocks.Judge;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IJudgeClient"/> that answers what the test scripts per question (escalated 'no-provider-decided' otherwise) and records what it was asked.</summary>
public sealed class FakeJudgeClient : IJudgeClient
{
    private readonly Dictionary<string, Result<JudgeAnswer>> _answers = new(StringComparer.Ordinal);
    private readonly List<JudgeInput> _asked = [];

    /// <summary>Every input asked, in order.</summary>
    public IReadOnlyList<JudgeInput> Asked => _asked;

    /// <summary>Answers <paramref name="question"/> with <paramref name="answer"/>.</summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer, or an error.</param>
    /// <returns>This fake.</returns>
    public FakeJudgeClient Answer(string question, Result<JudgeAnswer> answer)
    {
        _answers[question] = answer;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<JudgeAnswer>> Ask(JudgeInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        _asked.Add(input);
        return ValueTask.FromResult(_answers.TryGetValue(input.Question, out var answer) ? answer : new JudgeAnswer("no-provider-decided", Escalated: true));
    }
}
