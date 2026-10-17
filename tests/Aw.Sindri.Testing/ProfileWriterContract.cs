using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>What every <see cref="IProfileWriter"/> honours: a missing profile is created, the same entry twice changes nothing, a new port replaces a managed entry, and a hand-written entry is never replaced.</summary>
public abstract class ProfileWriterContract
{
    private const string Existing = "schemaVersion: 1\nuser: thor\n";
    private const string Version = "v4.9.0";

    /// <summary>A rig whose profile starts as <paramref name="text"/> (null for no file).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The rig.</returns>
    protected abstract ProfileWriterRig Create(string? text);

    /// <summary>No file: it is created, and the entry is in it.</summary>
    [Fact]
    public void A_missing_profile_is_created_with_the_managed_entry()
    {
        using var rig = Create(text: null);

        var outcome = rig.Writer.SetOnyxSource(3002, OnyxKeyPointers.Keychain, Version);

        Expect.Equal(ProfileWriteOutcome.Created, outcome.Value);
        Expect.True(rig.Text!.Contains("url: \"http://127.0.0.1:3002\"", StringComparison.Ordinal));
        Expect.True(rig.Text.Contains("managed: true", StringComparison.Ordinal));
        Expect.True(rig.Text.Contains("token: \"keychain:onyx/sindri-onyx\"", StringComparison.Ordinal));
    }

    /// <summary>An existing profile gains the entry; the same call again changes nothing.</summary>
    [Fact]
    public void The_same_entry_twice_is_changed_then_unchanged_and_the_text_is_stable()
    {
        using var rig = Create(Existing);
        var first = rig.Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, Version);
        var written = rig.Text;

        var second = rig.Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, Version);

        Expect.Equal(ProfileWriteOutcome.Changed, first.Value);
        Expect.Equal(ProfileWriteOutcome.Unchanged, second.Value);
        Expect.Equal(written, rig.Text);
    }

    /// <summary>A new port replaces a managed entry in place.</summary>
    [Fact]
    public void A_new_port_replaces_the_managed_entry()
    {
        using var rig = Create(Existing);
        _ = rig.Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, Version);

        var moved = rig.Writer.SetOnyxSource(3004, OnyxKeyPointers.Keychain, Version);

        Expect.Equal(ProfileWriteOutcome.Changed, moved.Value);
        Expect.True(rig.Text!.Contains("127.0.0.1:3004", StringComparison.Ordinal));
        Expect.True(!rig.Text.Contains("127.0.0.1:3000", StringComparison.Ordinal));
    }

    /// <summary>A hand-written onyx entry is refused and the profile is left as it was.</summary>
    [Fact]
    public void A_hand_written_onyx_entry_is_refused_and_left_alone()
    {
        const string handWritten = "schemaVersion: 1\nuser: thor\ncontext:\n  sources:\n    onyx:\n      url: \"http://127.0.0.1:3100\"\n      token: \"env:MY_ONYX\"\n      version: \"v4.8.0\"\n";
        using var rig = Create(handWritten);

        var outcome = rig.Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, Version);

        Expect.Equal("SND-CLI-003", outcome.Error.Code);
        Expect.Equal(handWritten, rig.Text);
    }
}
