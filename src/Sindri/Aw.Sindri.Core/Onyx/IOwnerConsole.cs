namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// The human at the terminal, for the parts of `onyx up` that need them (spec §4.1 steps 6 and 7). Prompts go to standard error so standard output stays the summary.
/// Every read returns null (or false) when input ends or is cancelled (end of file, Ctrl-C, a closed terminal): the step then records nothing.
/// </summary>
public interface IOwnerConsole
{
    /// <summary>Gets a value indicating whether a person can answer: standard input and standard error are both a terminal.</summary>
    public bool Interactive { get; }

    /// <summary>Shows a prompt and waits for Enter.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>True after Enter; false when input ended or was cancelled.</returns>
    public bool WaitForEnter(string prompt);

    /// <summary>Shows a prompt and reads a line, which is echoed as typed.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The line; null when input ended or was cancelled.</returns>
    public string? ReadLine(string prompt);

    /// <summary>Shows a prompt and reads a password key by key with echo off: no typed character is written anywhere.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The password (Backspace edits it); null when input ended or was cancelled.</returns>
    public string? ReadPassword(string prompt);
}
