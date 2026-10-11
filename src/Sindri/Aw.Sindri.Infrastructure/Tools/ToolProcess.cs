using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>
/// Runs a tool binary once: arguments as a list (no shell), optional stdin, stdout and stderr captured. At the timeout, or
/// when the caller cancels, the tool is stopped: a graceful call gets SIGTERM and a grace period first (ui-evidence releases
/// its machine locks on SIGTERM), and its tree is killed only if it is still running after the grace; any other call's tree
/// is killed at once. Once the tool exits, its output streams get a short grace to drain, so a child left holding them
/// can't turn a finished run into a timeout. A binary that can't be started or a timeout is
/// SND-BLK-001; a cancelled call throws OperationCanceledException after the tool is stopped; the caller maps exit codes.
/// </summary>
internal static class ToolProcess
{
    private static readonly TimeSpan _drainGrace = TimeSpan.FromSeconds(2);

    internal static async ValueTask<Result<ToolRun>> Run(ToolCall call, ToolSettings settings, CancellationToken ct)
    {
        var info = new ProcessStartInfo(call.Binary, call.Arguments)
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
            return SindriErrors.ToolUnavailable.With($"{call.Name} can't be started ({call.Binary}): {ex.Message}");
        }

        using (process)
        {
            return await Finish(process, call, call.Graceful ? settings.StopGrace : TimeSpan.Zero, ct).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<ToolRun>> Finish(Process process, ToolCall call, TimeSpan stopGrace, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(call.Timeout);
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        using var drain = new CancellationTokenSource();
        var copyOut = process.StandardOutput.BaseStream.CopyToAsync(stdout, drain.Token);
        var copyErr = process.StandardError.BaseStream.CopyToAsync(stderr, drain.Token);
        try
        {
            await Feed(process, call.Stdin, deadline.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Stop(process, stopGrace).ConfigureAwait(false);
            await drain.CancelAsync().ConfigureAwait(false);
            await Observe(copyOut, copyErr).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return SindriErrors.ToolUnavailable.With(string.Create(CultureInfo.InvariantCulture,
                $"{call.Name} didn't finish within {call.Timeout.TotalSeconds:0} s and was stopped"));
        }

        drain.CancelAfter(_drainGrace);
        await Observe(copyOut, copyErr).ConfigureAwait(false);
        return new ToolRun(process.ExitCode, stdout.ToArray(), System.Text.Encoding.UTF8.GetString(stderr.ToArray()));
    }

    // Writes stdin and closes it; a tool that exits without reading it is fine (its exit code still counts).
    private static async Task Feed(Process process, byte[]? stdin, CancellationToken ct)
    {
        try
        {
            if (stdin is not null)
            {
                await process.StandardInput.BaseStream.WriteAsync(stdin, ct).ConfigureAwait(false);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // the tool closed its stdin early; what it printed and its exit code decide
        }
    }

    // Waits for both output readers; a reader cut off by the drain grace keeps what it copied so far.
    private static async Task Observe(params Task[] readers)
    {
        foreach (var reader in readers)
        {
            try
            {
                await reader.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // cut off: the buffer holds what arrived
            }
        }
    }

    // A zero grace kills the tree at once; otherwise SIGTERM first and the tree is killed if it outlives the grace.
    private static async Task Stop(Process process, TimeSpan grace)
    {
        if (grace > TimeSpan.Zero && !process.HasExited)
        {
            Signal.Terminate(process.Id);
            using var wait = new CancellationTokenSource(grace);
            try
            {
                await process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                // it outlived the grace
            }
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // it exited in the meantime
        }
    }
}
