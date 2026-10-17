using System.Text;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>
/// A terminal for `onyx up` tests: answers come from a queue (null is end of input), prompts and echoed lines are written to <see cref="Terminal"/>, and a password
/// is never echoed. With nothing queued, Enter is pressed. Held to <see cref="OwnerConsoleContract"/>.
/// </summary>
public sealed class FakeOwnerConsole : IOwnerConsole
{
    private readonly Queue<string?> _answers = new();
    private readonly StringBuilder _terminal = new();

    /// <summary>Gets or sets a value indicating whether a person can answer.</summary>
    public bool Interactive { get; set; } = true;

    /// <summary>Gets the prompts shown, in order.</summary>
    public IList<string> Prompts { get; } = [];

    /// <summary>Gets everything the terminal showed: prompts and echoed lines, never a password.</summary>
    public string Terminal => _terminal.ToString();

    /// <summary>Queues answers for the next reads; null makes that read see end of input.</summary>
    /// <param name="answers">The answers.</param>
    public void Feed(params string?[] answers)
    {
        foreach (var answer in answers)
        {
            _answers.Enqueue(answer);
        }
    }

    /// <inheritdoc />
    public bool WaitForEnter(string prompt)
    {
        Show(prompt);
        var answer = _answers.Count > 0 ? _answers.Dequeue() : string.Empty;
        _ = _terminal.AppendLine();
        return answer is not null;
    }

    /// <inheritdoc />
    public string? ReadLine(string prompt)
    {
        Show(prompt);
        var answer = _answers.Count > 0 ? _answers.Dequeue() : null;
        _ = _terminal.AppendLine(answer);   // echoed, as a terminal does
        return answer;
    }

    /// <inheritdoc />
    public string? ReadPassword(string prompt)
    {
        Show(prompt);
        var answer = _answers.Count > 0 ? _answers.Dequeue() : null;
        _ = _terminal.AppendLine();   // the Enter, with nothing typed shown
        return answer;
    }

    private void Show(string prompt)
    {
        Prompts.Add(prompt);
        _ = _terminal.Append(prompt);
    }
}
