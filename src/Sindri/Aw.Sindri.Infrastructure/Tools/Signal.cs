using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>Sends SIGTERM through /bin/kill (macOS and Linux); .NET's Process.Kill only sends SIGKILL, which a tool can't handle.</summary>
internal static class Signal
{
    /// <summary>Asks <paramref name="pid"/> to terminate; a process that is already gone, or no /bin/kill, is not an error (the caller falls back to SIGKILL).</summary>
    /// <param name="pid">The process id.</param>
    internal static void Terminate(int pid)
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/bin/kill"))
        {
            return;
        }

        try
        {
            using var kill = Process.Start(new ProcessStartInfo("/bin/kill", ["-TERM", pid.ToString(System.Globalization.CultureInfo.InvariantCulture)]) { UseShellExecute = false });
            _ = kill?.WaitForExit(TimeSpan.FromSeconds(5));
        }
        catch (Win32Exception)
        {
            // no signal sent; the grace runs out and SIGKILL follows
        }
    }
}
