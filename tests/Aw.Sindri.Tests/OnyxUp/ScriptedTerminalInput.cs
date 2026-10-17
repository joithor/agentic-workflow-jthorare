using Aw.Sindri.Host.Onyx;

namespace Aw.Sindri.Tests.OnyxUp;

/// <summary>A keyboard that types what it is told: each answer's characters one key at a time, then Enter; null is end of input. A '\b' is the Backspace key and '\u0003' is Ctrl-C.</summary>
internal sealed class ScriptedTerminalInput : ITerminalInput
{
    private readonly Queue<string?> _answers = new();
    private readonly Queue<ConsoleKeyInfo?> _keys = new();

    public bool Interactive { get; set; } = true;

    public void Feed(params string?[] answers)
    {
        foreach (var answer in answers)
        {
            _answers.Enqueue(answer);
        }
    }

    public ConsoleKeyInfo? ReadKey()
    {
        if (_keys.Count == 0)
        {
            if (!_answers.TryDequeue(out var answer) || answer is null)
            {
                return null;
            }

            foreach (var c in answer)
            {
                _keys.Enqueue(Key(c));
            }

            _keys.Enqueue(new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift: false, alt: false, control: false));
        }

        return _keys.Dequeue();
    }

    public string? ReadLine() => _answers.TryDequeue(out var answer) ? answer : null;

    private static ConsoleKeyInfo Key(char c) => c switch
    {
        '\b' => new ConsoleKeyInfo(c, ConsoleKey.Backspace, shift: false, alt: false, control: false),
        '\u0003' => new ConsoleKeyInfo(c, ConsoleKey.C, shift: false, alt: false, control: true),
        _ => new ConsoleKeyInfo(c, ConsoleKey.NoName, shift: false, alt: false, control: false),
    };
}
