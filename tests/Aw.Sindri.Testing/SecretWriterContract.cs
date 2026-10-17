using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>What every <see cref="Core.Secrets.ISecretWriter"/> honours: the pointer it returns reads back the value, never shows it, and an empty key is SND-ONYX-009.</summary>
public abstract class SecretWriterContract
{
    /// <summary>A key shaped like Onyx's (url-safe characters only).</summary>
    protected const string Key = "on_Zk3-9fQ_aBcDeFg1234567890xyz";

    /// <summary>A rig with a working store.</summary>
    /// <returns>The rig.</returns>
    protected abstract SecretWriterRig Create();

    /// <summary>The pointer reads back exactly the stored value.</summary>
    [Fact]
    public void The_pointer_reads_back_exactly_what_was_stored()
    {
        using var rig = Create();

        var stored = rig.Writer.Store(Key);

        Expect.True(stored.IsSuccess);
        Expect.Equal(Key, rig.Read(stored.Value));
    }

    /// <summary>The pointer's text holds no part of the value.</summary>
    [Fact]
    public void The_pointer_text_never_shows_the_value()
    {
        using var rig = Create();

        var stored = rig.Writer.Store(Key);

        Expect.True(!stored.Value.ToString().Contains(Key, StringComparison.Ordinal));
    }

    /// <summary>Storing the same value twice reads back the same value.</summary>
    [Fact]
    public void Storing_twice_replaces_the_first_value()
    {
        using var rig = Create();
        _ = rig.Writer.Store("on_first-key-value-000");

        var again = rig.Writer.Store(Key);

        Expect.Equal(Key, rig.Read(again.Value));
    }

    /// <summary>An empty key is refused as SND-ONYX-009.</summary>
    [Fact]
    public void An_empty_key_is_009()
    {
        using var rig = Create();

        var stored = rig.Writer.Store(string.Empty);

        Expect.Equal("SND-ONYX-009", stored.Error.Code);
    }
}
