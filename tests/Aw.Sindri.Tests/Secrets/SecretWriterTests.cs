using System.Runtime.Versioning;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

// The secret writer (spec §5.3): the key goes to the keychain or libsecret on standard input, else to an owner-only file; it never goes into an argument list.
[UnsupportedOSPlatform("windows")]
public sealed class SecretWriterTests : IDisposable
{
    private const string Key = "on_Zk3-9fQ_aBcDeFg1234567890xyz";
    private static readonly ToolRun _exitedOk = new(ToolOutcome.Exited, 0, string.Empty);
    private static readonly ToolRun _exitedBad = new(ToolOutcome.Exited, 1, string.Empty);

    private readonly string _home = Directory.CreateTempSubdirectory("sindri-secret-").FullName;
    private readonly FakeSecretPointers _pointers = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string FilePointerText => "file:" + _home + "/secrets/onyx-sindri-onyx";

    [Fact, Trait("Lane", "unit")]
    public void On_macOS_the_secret_is_in_stdin_and_never_in_argv()
    {
        var runner = new FakeToolRunner(_exitedOk);
        _pointers.Values["keychain:onyx/sindri-onyx"] = Key;

        var stored = new SecretWriter(runner, _pointers, _home, KeychainPlatform.MacOS).Store(Key);

        Assert.Equal("keychain:onyx/sindri-onyx", stored.Value.ToString());
        var call = Assert.Single(runner.Calls);
        Assert.Equal("security", call.FileName);
        Assert.Equal(["-i"], call.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(10), call.Timeout);
        Assert.Equal("add-generic-password -U -s onyx -a sindri-onyx -w \"" + Key + "\"\n", Assert.Single(runner.StandardInputs));
        Assert.All(call.Arguments, argument => Assert.DoesNotContain(Key, argument, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void On_Linux_secret_tool_store_gets_the_value_on_stdin_only()
    {
        var runner = new FakeToolRunner(_exitedOk);
        _pointers.Values["keychain:onyx/sindri-onyx"] = Key;

        var stored = new SecretWriter(runner, _pointers, _home, KeychainPlatform.Linux).Store(Key);

        Assert.Equal("keychain:onyx/sindri-onyx", stored.Value.ToString());
        var call = Assert.Single(runner.Calls);
        Assert.Equal("secret-tool", call.FileName);
        Assert.Equal(["store", "--label", "onyx/sindri-onyx", "service", "onyx", "account", "sindri-onyx"], call.Arguments);
        Assert.Equal(Key, Assert.Single(runner.StandardInputs));
        Assert.All(call.Arguments, argument => Assert.DoesNotContain(Key, argument, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_real_child_process_never_shows_the_secret_in_its_argv()
    {
        var bin = Directory.CreateDirectory(Path.Combine(_home, "bin")).FullName;
        var capture = Directory.CreateDirectory(Path.Combine(_home, "capture")).FullName;
        var script = Path.Combine(bin, "security");
        File.WriteAllText(script, "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$AW_STUB_OUT/argv\"\ncat > \"$AW_STUB_OUT/stdin\"\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var runner = new StubDirectoryRunner(new ProcessToolRunner(), bin, new Dictionary<string, string?>(StringComparer.Ordinal) { ["AW_STUB_OUT"] = capture });
        _pointers.Values["keychain:onyx/sindri-onyx"] = Key;

        var stored = new SecretWriter(runner, _pointers, _home, KeychainPlatform.MacOS).Store(Key);

        Assert.True(stored.IsSuccess);
        Assert.Equal("-i\n", File.ReadAllText(Path.Combine(capture, "argv")));   // the child's own argv: "-i" and nothing else
        Assert.Equal("add-generic-password -U -s onyx -a sindri-onyx -w \"" + Key + "\"\n", File.ReadAllText(Path.Combine(capture, "stdin")));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_failing_keychain_falls_back_to_a_0600_file_in_a_0700_directory_and_returns_the_file_pointer()
    {
        var stored = new SecretWriter(new FakeToolRunner(_exitedBad), _pointers, _home, KeychainPlatform.Linux).Store(Key);

        Assert.Equal(FilePointerText, stored.Value.ToString());
        var file = Path.Combine(_home, "secrets", "onyx-sindri-onyx");
        Assert.Equal(Key, File.ReadAllText(file));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.Combine(_home, "secrets")));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(true)]
    [InlineData(false)]
    public void A_missing_or_hung_tool_falls_back_to_the_file(bool missing)
    {
        var stored = new SecretWriter(new FakeToolRunner(missing ? ToolRun.Missing : ToolRun.TimedOut), _pointers, _home, KeychainPlatform.Linux).Store(Key);

        Assert.Equal(FilePointerText, stored.Value.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public void An_unsupported_platform_writes_the_file_without_running_anything()
    {
        var runner = new FakeToolRunner(_exitedOk);

        var stored = new SecretWriter(runner, _pointers, _home, KeychainPlatform.Unsupported).Store(Key);

        Assert.Equal(FilePointerText, stored.Value.ToString());
        Assert.Empty(runner.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_keychain_that_exits_0_but_reads_back_another_value_falls_back_to_the_file()
    {
        var stub = new StubKeychainRunner(KeychainPlatform.MacOS) { KeepsNothing = true };
        var pointers = new PointerSecrets(static _ => null, stub, KeychainPlatform.MacOS);

        var stored = new SecretWriter(stub, pointers, _home, KeychainPlatform.MacOS).Store(Key);

        Assert.Equal(FilePointerText, stored.Value.ToString());
        Assert.Equal(Key, File.ReadAllText(Path.Combine(_home, "secrets", "onyx-sindri-onyx")));
    }

    [Fact, Trait("Lane", "integration")]
    public void Both_failing_is_SND_ONYX_009_and_names_no_value()
    {
        File.WriteAllText(Path.Combine(_home, "secrets"), "a file where the directory should be");

        var stored = new SecretWriter(new FakeToolRunner(ToolRun.Missing), _pointers, _home, KeychainPlatform.Linux).Store(Key);

        Assert.Equal("SND-ONYX-009", stored.Error.Code);
        Assert.DoesNotContain(Key, stored.Error.Message, StringComparison.Ordinal);
        Assert.Contains(OnyxFixes.SecretWriteFailed, stored.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_symlink_at_the_fallback_path_is_refused()
    {
        var secrets = Directory.CreateDirectory(Path.Combine(_home, "secrets")).FullName;
        var target = Path.Combine(_home, "elsewhere");
        File.WriteAllText(target, "original");
        _ = File.CreateSymbolicLink(Path.Combine(secrets, "onyx-sindri-onyx"), target);

        var stored = new SecretWriter(new FakeToolRunner(ToolRun.Missing), _pointers, _home, KeychainPlatform.Linux).Store(Key);

        Assert.Equal("SND-ONYX-009", stored.Error.Code);
        Assert.Equal("original", File.ReadAllText(target));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("on_a\"b")]
    [InlineData("on_a\\b")]
    [InlineData("on_a\nb")]
    [InlineData("on_a\rb")]
    public void A_value_with_quote_backslash_or_newline_is_refused_on_macOS_before_any_run(string value)
    {
        var runner = new FakeToolRunner(_exitedOk);

        var stored = new SecretWriter(runner, _pointers, _home, KeychainPlatform.MacOS).Store(value);

        Assert.Equal("SND-ONYX-009", stored.Error.Code);
        Assert.Empty(runner.Calls);
        Assert.False(Directory.Exists(Path.Combine(_home, "secrets")));
    }

    // Runs a tool only from the stand-in directory, by its full path, so the real `security` and the user's keychain are never reached (PATH is not consulted: a missing stand-in is a missing tool).
    private sealed class StubDirectoryRunner(IToolRunner inner, string directory, IReadOnlyDictionary<string, string?> environment) : IToolRunner
    {
        public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
            => File.Exists(Path.Combine(directory, fileName))
                ? inner.Run(Path.Combine(directory, fileName), arguments, timeout, (options ?? new ToolRunOptions()) with { Environment = environment }, cancellationToken)
                : ToolRun.Missing;
    }
}
