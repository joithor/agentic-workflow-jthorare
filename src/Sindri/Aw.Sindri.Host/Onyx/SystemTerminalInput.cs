namespace Aw.Sindri.Host.Onyx;

/// <summary>ITerminalInput over System.Console. Reading a key needs a terminal, so callers check <see cref="Interactive"/> first.</summary>
internal sealed class SystemTerminalInput : ITerminalInput
{
    /// <inheritdoc />
    public bool Interactive => !Console.IsInputRedirected && !Console.IsErrorRedirected;

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey()
    {
        _ = Console.KeyAvailable;
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public string? ReadLine() => Console.ReadLine();
}
