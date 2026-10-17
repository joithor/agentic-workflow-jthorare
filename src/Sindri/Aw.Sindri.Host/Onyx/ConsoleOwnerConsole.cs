using System.Text;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Host.Onyx;

/// <summary>IOwnerConsole over the process's terminal: prompts on standard error, a hidden read taken key by key so no typed character is ever written.</summary>
/// <param name="error">Where prompts go.</param>
/// <param name="input">The keyboard.</param>
internal sealed class ConsoleOwnerConsole(TextWriter error, ITerminalInput input) : IOwnerConsole
{
    /// <inheritdoc />
    public bool Interactive => input.Interactive;

    /// <inheritdoc />
    public bool WaitForEnter(string prompt)
    {
        error.Write(prompt + " ");
        return input.ReadLine() is not null;
    }

    /// <inheritdoc />
    public string? ReadLine(string prompt)
    {
        error.Write(prompt);
        return input.ReadLine();
    }

    /// <inheritdoc />
    public string? ReadPassword(string prompt)
    {
        error.Write(prompt);
        var typed = new StringBuilder();
        while (true)
        {
            var key = input.ReadKey();
            if (key is not { } pressed || IsInterrupt(pressed))
            {
                error.WriteLine();
                return null;
            }

            if (pressed.Key == ConsoleKey.Enter)
            {
                error.WriteLine();
                return typed.ToString();
            }

            if (pressed.Key == ConsoleKey.Backspace)
            {
                typed.Length = Math.Max(0, typed.Length - 1);
            }
            else if (!char.IsControl(pressed.KeyChar))
            {
                _ = typed.Append(pressed.KeyChar);   // kept, never echoed
            }
        }
    }

    private static bool IsInterrupt(ConsoleKeyInfo key) => key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control);
}
