using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IBrowser over `open` (macOS) and `xdg-open` (Linux): the address is the tool's only argument, and only http addresses are opened.</summary>
/// <param name="runner">Runs the tool.</param>
/// <param name="os">Which command to run; null takes this machine's.</param>
internal sealed class SystemBrowser(IToolRunner runner, DesktopOs? os = null) : IBrowser
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    private readonly DesktopOs _os = os ?? Detect();

    /// <inheritdoc />
    public bool Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var tool = _os switch
        {
            DesktopOs.MacOS => "open",
            DesktopOs.Linux => "xdg-open",
            DesktopOs.Unsupported => null,
            _ => throw new ArgumentOutOfRangeException(nameof(url), _os, "unknown desktop"),
        };
        if (tool is null || !(string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return false;
        }

        return runner.Run(tool, [url.AbsoluteUri], _timeout) is { Outcome: ToolOutcome.Exited, ExitCode: 0 };
    }

    private static DesktopOs Detect() => OperatingSystem.IsMacOS() ? DesktopOs.MacOS : OperatingSystem.IsLinux() ? DesktopOs.Linux : DesktopOs.Unsupported;
}
