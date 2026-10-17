namespace Aw.Sindri.Host.Onyx;

/// <summary>The keyboard under <see cref="ConsoleOwnerConsole"/>: the seam that lets the echo-off password read be tested without a terminal.</summary>
internal interface ITerminalInput
{
    /// <summary>Gets a value indicating whether standard input and standard error are both a terminal.</summary>
    public bool Interactive { get; }

    /// <summary>Reads one key without echoing it; null at end of input.</summary>
    /// <returns>The key.</returns>
    public ConsoleKeyInfo? ReadKey();

    /// <summary>Reads a line, echoed by the terminal; null at end of input.</summary>
    /// <returns>The line.</returns>
    public string? ReadLine();
}
