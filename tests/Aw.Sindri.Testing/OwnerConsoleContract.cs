using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>What every <see cref="Core.Onyx.IOwnerConsole"/> honours: prompts shown, lines read, a secret read that is never written anywhere, and null (or false) at end of input.</summary>
public abstract class OwnerConsoleContract
{
    /// <summary>What the tests type at a hidden prompt.</summary>
    protected const string Typed = "zebra-quartz-lantern-41";

    /// <summary>A console with an interactive terminal.</summary>
    /// <returns>The rig.</returns>
    protected abstract OwnerConsoleRig Create();

    /// <summary>A hidden read comes back as typed and none of it is written to the terminal.</summary>
    [Fact]
    public void A_hidden_read_is_returned_as_typed_and_never_written_to_the_terminal()
    {
        var rig = Create();
        rig.Feed(Typed);

        var read = rig.Console.ReadPassword("Onyx password: ");

        Expect.Equal(Typed, read);
        Expect.True(rig.Terminal.Contains("Onyx password: ", StringComparison.Ordinal));
        Expect.True(!rig.Terminal.Contains("zebra", StringComparison.Ordinal));
        Expect.True(!rig.Terminal.Contains("lantern", StringComparison.Ordinal));
    }

    /// <summary>A line comes back as typed, after its prompt.</summary>
    [Fact]
    public void A_line_is_returned_as_typed_after_its_prompt()
    {
        var rig = Create();
        rig.Feed("owner@example.com");

        var read = rig.Console.ReadLine("Onyx email: ");

        Expect.Equal("owner@example.com", read);
        Expect.True(rig.Terminal.Contains("Onyx email: ", StringComparison.Ordinal));
    }

    /// <summary>Enter ends the wait.</summary>
    [Fact]
    public void Enter_ends_the_wait()
    {
        var rig = Create();
        rig.Feed(string.Empty);

        Expect.True(rig.Console.WaitForEnter("Press Enter"));
        Expect.True(rig.Terminal.Contains("Press Enter", StringComparison.Ordinal));
    }

    /// <summary>End of input is null for a read and false for the wait.</summary>
    [Fact]
    public void End_of_input_is_null_or_false()
    {
        var rig = Create();
        rig.Feed(null, null, null);

        Expect.True(rig.Console.ReadLine("email: ") is null);
        Expect.True(rig.Console.ReadPassword("secret: ") is null);
        Expect.True(!rig.Console.WaitForEnter("Enter"));
    }
}
