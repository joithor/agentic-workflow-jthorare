using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.OnyxUp;

// What only the file writer can get wrong: the file's mode, its atomic replacement and a profile that can't be read.
[Lane(TestLane.Integration)]
[UnsupportedOSPlatform("windows")]
public sealed class FileProfileWriterTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("sindri-profile-").FullName;

    private string ProfilePath => Path.Combine(_home, "profile", "profile.yaml");

    private FileProfileWriter Writer => new(new SindriSettings(_home, ProfilePath), "thor");

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public void A_created_profile_is_0600_and_leaves_no_temporary_file()
    {
        var outcome = Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, "v4.9.0");

        Assert.Equal(ProfileWriteOutcome.Created, outcome.Value);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ProfilePath));
        Assert.Equal([ProfilePath], Directory.GetFiles(Path.GetDirectoryName(ProfilePath)!));
        Assert.StartsWith("schemaVersion: 1\nuser: thor\n", File.ReadAllText(ProfilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void A_second_identical_write_does_not_touch_the_file()
    {
        _ = Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, "v4.9.0");
        var before = File.GetLastWriteTimeUtc(ProfilePath);
        File.SetLastWriteTimeUtc(ProfilePath, before.AddMinutes(-10));
        var marked = File.GetLastWriteTimeUtc(ProfilePath);

        var outcome = Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, "v4.9.0");

        Assert.Equal(ProfileWriteOutcome.Unchanged, outcome.Value);
        Assert.Equal(marked, File.GetLastWriteTimeUtc(ProfilePath));
    }

    [Fact]
    public void An_existing_profile_keeps_its_comments_on_disk()
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        File.WriteAllText(ProfilePath, "# mine\nschemaVersion: 1\nuser: thor   # me\n");

        var outcome = Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, "v4.9.0");

        Assert.Equal(ProfileWriteOutcome.Changed, outcome.Value);
        Assert.StartsWith("# mine\nschemaVersion: 1\nuser: thor   # me\ncontext:\n", File.ReadAllText(ProfilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_that_cannot_be_read_is_SND_PROFILE_001_and_nothing_is_written()
    {
        _ = Directory.CreateDirectory(ProfilePath);   // a directory where the file should be

        var outcome = Writer.SetOnyxSource(3000, OnyxKeyPointers.Keychain, "v4.9.0");

        Assert.Equal("SND-PROFILE-001", outcome.Error.Code);
        Assert.True(Directory.Exists(ProfilePath));
    }
}
