using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IBrowser over `open` (macOS) and `xdg-open` (Linux).</summary>
/// <param name="runner">Runs the tool.</param>
/// <param name="os">Which command to run; null takes this machine's.</param>
internal sealed class SystemBrowser(IToolRunner runner, DesktopOs? os = null) : IBrowser
{
    private readonly DesktopOs _os = os ?? Detect();

    /// <inheritdoc />
    public bool Open(Uri url)
    {
        _ = (runner, _os, url);
        throw new NotSupportedException();
    }

    private static DesktopOs Detect() => OperatingSystem.IsMacOS() ? DesktopOs.MacOS : OperatingSystem.IsLinux() ? DesktopOs.Linux : DesktopOs.Unsupported;
}
