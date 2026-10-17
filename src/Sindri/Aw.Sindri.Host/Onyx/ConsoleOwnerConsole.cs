using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Host.Onyx;

/// <summary>IOwnerConsole over the process's terminal: prompts on standard error, the password read key by key with echo off.</summary>
/// <param name="error">Where prompts go.</param>
/// <param name="input">The keyboard.</param>
internal sealed class ConsoleOwnerConsole(TextWriter error, ITerminalInput input) : IOwnerConsole
{
    /// <inheritdoc />
    public bool Interactive => input.Interactive;

    /// <inheritdoc />
    public bool WaitForEnter(string prompt)
    {
        _ = (error, prompt);
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public string? ReadLine(string prompt)
    {
        _ = prompt;
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public string? ReadPassword(string prompt)
    {
        _ = prompt;
        throw new NotSupportedException();
    }
}
