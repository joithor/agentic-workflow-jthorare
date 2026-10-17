using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>
/// A pair of stand-in tools that implements the keychain commands Sindri runs over one in-memory entry: on macOS `security -i` (an add-generic-password command on
/// standard input) and `security find-generic-password ... -w`; on Linux `secret-tool store ...` (the value on standard input) and `secret-tool lookup ...`. It records
/// every call's arguments and standard input and never starts a process.
/// </summary>
/// <param name="platform">Which tool pair it plays.</param>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because IToolRunner is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class StubKeychainRunner(KeychainPlatform platform) : IToolRunner
{
    /// <summary>Gets or sets the entry's value, or null when none is stored.</summary>
    public string? Entry { get; set; }

    /// <summary>Gets or sets a value indicating whether the store commands fail with a non-zero exit (a locked keychain).</summary>
    public bool Failing { get; set; }

    /// <summary>Gets or sets a value indicating whether a store command exits 0 but keeps nothing (as `security -i` can after an error).</summary>
    public bool KeepsNothing { get; set; }

    /// <summary>Gets the calls made: the tool, its arguments and the standard input it was given.</summary>
    public IList<(string FileName, IReadOnlyList<string> Arguments, string? StandardInput)> Calls { get; } = [];

    /// <inheritdoc />
    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var input = options?.StandardInput;
        Calls.Add((fileName, [.. arguments], input));
        return (platform, fileName) switch
        {
            (KeychainPlatform.MacOS, "security") when arguments.SequenceEqual(["-i"], StringComparer.Ordinal) => Store(MacValue(input)),
            (KeychainPlatform.MacOS, "security") when arguments is ["find-generic-password", "-s", OnyxKeyPointers.Service, "-a", OnyxKeyPointers.Account, "-w"] => Lookup(),
            (KeychainPlatform.Linux, "secret-tool") when arguments is ["store", "--label", "onyx/sindri-onyx", "service", OnyxKeyPointers.Service, "account", OnyxKeyPointers.Account] => Store(input),
            (KeychainPlatform.Linux, "secret-tool") when arguments is ["lookup", "service", OnyxKeyPointers.Service, "account", OnyxKeyPointers.Account] => Lookup(),
            _ => new ToolRun(ToolOutcome.Exited, 2, string.Empty),
        };
    }

    // `add-generic-password -U -s onyx -a sindri-onyx -w "<value>"`: the quoted word after -w.
    private static string? MacValue(string? script)
    {
        const string marker = " -w \"";
        var start = script?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
        if (start < 0 || script is null)
        {
            return null;
        }

        var from = start + marker.Length;
        var end = script.IndexOf('"', from);
        return end < 0 ? null : script[from..end];
    }

    private ToolRun Store(string? value)
    {
        if (Failing || value is null)
        {
            return new ToolRun(ToolOutcome.Exited, 1, string.Empty);
        }

        if (!KeepsNothing)
        {
            Entry = value;
        }

        return new ToolRun(ToolOutcome.Exited, 0, string.Empty);
    }

    private ToolRun Lookup() => Entry is { } value ? new ToolRun(ToolOutcome.Exited, 0, value + "\n") : new ToolRun(ToolOutcome.Exited, 44, string.Empty);
}
