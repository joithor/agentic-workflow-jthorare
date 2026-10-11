using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>
/// Runs a tool binary once: arguments as a list (no shell), optional stdin, stdout and stderr captured, a timeout after
/// which the process tree is killed. A binary that can't be started or a timeout is SND-BLK-001; the caller maps exit codes.
/// </summary>
internal static class ToolProcess
{
    internal static async ValueTask<Result<ToolRun>> Run(string binary, IReadOnlyList<string> arguments, byte[]? stdin, TimeSpan timeout, CancellationToken ct)
    {
        var info = new ProcessStartInfo(binary, arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.Environment["AW_LOG_LEVEL"] = "warning";   // the tools log JSON lines on stderr; keep only what matters
        Process process;
        try
        {
            process = Process.Start(info) ?? throw new InvalidOperationException("Process.Start returned no process");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return SindriErrors.ToolUnavailable.With($"{Path.GetFileName(binary)} can't be started ({binary}): {ex.Message}");
        }

        using (process)
        {
            return await Finish(process, binary, stdin, timeout, ct).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<ToolRun>> Finish(Process process, string binary, byte[]? stdin, TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        using var stdout = new MemoryStream();
        var copyOut = process.StandardOutput.BaseStream.CopyToAsync(stdout, deadline.Token);
        var readErr = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            if (stdin is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(stdin, deadline.Token).ConfigureAwait(false);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            await copyOut.ConfigureAwait(false);
            var stderr = await readErr.ConfigureAwait(false);
            return new ToolRun(process.ExitCode, stdout.ToArray(), stderr);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            Kill(process);
            ct.ThrowIfCancellationRequested();
            return SindriErrors.ToolUnavailable.With(ex is IOException
                ? $"{Path.GetFileName(binary)} closed its pipes early: {ex.Message}"
                : string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(binary)} didn't finish within {timeout.TotalSeconds:0} s and was stopped"));
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // it exited on its own in the meantime
        }
    }
}
