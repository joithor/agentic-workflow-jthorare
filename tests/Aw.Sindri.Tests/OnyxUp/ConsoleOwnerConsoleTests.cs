using Aw.Kernel.Testing;
using Aw.Sindri.Host.Onyx;

namespace Aw.Sindri.Tests.OnyxUp;

// What only the terminal adapter can get wrong: editing a hidden read key by key without ever writing it, and Ctrl-C.
[Lane(TestLane.Unit)]
public sealed class ConsoleOwnerConsoleTests : IDisposable
{
    private readonly StringWriter _terminal = new();
    private readonly ScriptedTerminalInput _keyboard = new();

    private ConsoleOwnerConsole Console => new(_terminal, _keyboard);

    public void Dispose() => _terminal.Dispose();

    [Fact]
    public void Backspace_edits_the_hidden_read_and_nothing_typed_is_written()
    {
        _keyboard.Feed("qwxz\bvk9");

        var read = Console.ReadPassword("Secret: ");

        Assert.Equal("qwxvk9", read);
        Assert.DoesNotContain("qw", _terminal.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("vk9", _terminal.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ctrl_C_while_typing_a_hidden_read_is_null()
    {
        _keyboard.Feed("ab\u0003cd");

        Assert.Null(Console.ReadPassword("Secret: "));
    }

    [Fact]
    public void Interactive_follows_the_keyboard()
    {
        _keyboard.Interactive = false;

        Assert.False(Console.Interactive);
    }
}
