using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>
/// ISecretWriter (spec §5.3) over the macOS keychain (`security -i`, the command on standard input so the key is never in an argument list), libsecret on Linux
/// (`secret-tool store`, the key on standard input) and an owner-only file under the Sindri home when neither takes it. After a keychain write it reads the entry back
/// through <see cref="ISecretPointers"/> and falls back if it isn't the value (`security -i` can exit 0 after an error). It writes exactly one entry, `onyx/sindri-onyx`.
/// It shares nothing with the reader (<see cref="PointerSecrets"/>) beyond <see cref="OnyxKeyPointers"/>: the writer's command lines are its own, and the agreement
/// tests hold the two to the same entry.
/// </summary>
/// <param name="runner">Runs security and secret-tool.</param>
/// <param name="pointers">Reads the entry back.</param>
/// <param name="sindriHome">Where the fallback file goes.</param>
/// <param name="platform">Which store to write; null takes this machine's.</param>
internal sealed class SecretWriter(IToolRunner runner, ISecretPointers pointers, string sindriHome, KeychainPlatform? platform = null) : ISecretWriter
{
    // A keychain answers in milliseconds; a locked one's prompt must not hold `onyx up`.
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly char[] _unquotable = ['"', '\\', '\r', '\n'];

    private readonly KeychainPlatform _platform = platform ?? Detect();

    /// <inheritdoc />
    public Result<SecretPointer> Store(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return Failed("there is no key to store");
        }

        // The macOS command is one quoted word on standard input; a character that would end or alter it is refused rather than escaped (an Onyx key is url-safe, so this never fires for a real one).
        if (_platform == KeychainPlatform.MacOS && value.IndexOfAny(_unquotable) >= 0)
        {
            return Failed("the key holds a quote, backslash or line break the keychain command can't carry");
        }

        return InKeychain(value) ? OnyxKeyPointers.Keychain : InFile(value);
    }

    private static KeychainPlatform Detect()
        => OperatingSystem.IsMacOS() ? KeychainPlatform.MacOS : OperatingSystem.IsLinux() ? KeychainPlatform.Linux : KeychainPlatform.Unsupported;

    private bool InKeychain(string value)
    {
        var run = _platform switch
        {
            KeychainPlatform.MacOS => runner.Run(
                "security",
                ["-i"],
                _timeout,
                new ToolRunOptions(StandardInput: $"add-generic-password -U -s {OnyxKeyPointers.Service} -a {OnyxKeyPointers.Account} -w \"{value}\"\n")),
            KeychainPlatform.Linux => runner.Run(
                "secret-tool",
                ["store", "--label", $"{OnyxKeyPointers.Service}/{OnyxKeyPointers.Account}", "service", OnyxKeyPointers.Service, "account", OnyxKeyPointers.Account],
                _timeout,
                new ToolRunOptions(StandardInput: value)),
            KeychainPlatform.Unsupported => null,
            _ => throw new ArgumentOutOfRangeException(nameof(value), _platform, "unknown platform"),
        };
        return run is { Outcome: ToolOutcome.Exited, ExitCode: 0 }
            && pointers.Resolve(OnyxKeyPointers.Keychain) is { IsSuccess: true } read
            && string.Equals(read.Value, value, StringComparison.Ordinal);
    }

    // The fallback: <home>/secrets/onyx-sindri-onyx, a regular file made 0600 from the start in a 0700 directory; a link or a directory in its place is refused.
    private Result<SecretPointer> InFile(string value)
    {
        try
        {
            var path = OnyxKeyPointers.FilePath(sindriHome);
            var directory = Path.GetDirectoryName(path)!;   // why: the path always has a directory
            OwnerOnly.EnsureDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            if (Directory.Exists(path) || new FileInfo(path).LinkTarget is not null)
            {
                return Failed("the fallback file's place holds a link or a directory, not a file");
            }

            OwnerOnly.WriteAtomically(path, Encoding.UTF8.GetBytes(value));
            return OnyxKeyPointers.FileFor(sindriHome);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed($"the keychain didn't take the key and the fallback file under the Sindri home couldn't be written ({exception.GetType().Name})");
        }
    }

    private static Error Failed(string what) => SindriErrors.OnyxSecretWriteFailed.With($"{what}; {OnyxFixes.SecretWriteFailed}");
}
