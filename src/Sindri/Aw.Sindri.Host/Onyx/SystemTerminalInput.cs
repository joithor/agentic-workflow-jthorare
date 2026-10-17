namespace Aw.Sindri.Host.Onyx;

/// <summary>ITerminalInput over System.Console. A key is read with no echo and with Ctrl-C delivered as a key (so a hidden read ends cleanly); callers check <see cref="Interactive"/> first.</summary>
internal sealed class SystemTerminalInput : ITerminalInput
{
    /// <inheritdoc />
    public bool Interactive => !Console.IsInputRedirected && !Console.IsErrorRedirected;

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey()
    {
        try
        {
            Console.TreatControlCAsInput = true;
            return Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            return null;   // input isn't a terminal after all
        }
        finally
        {
            Console.TreatControlCAsInput = false;
        }
    }

    /// <inheritdoc />
    public string? ReadLine() => Console.ReadLine();
}
