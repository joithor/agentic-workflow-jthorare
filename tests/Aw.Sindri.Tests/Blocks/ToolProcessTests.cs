using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Tools;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>The tool runner against scripted binaries: a timeout stops the tool gracefully, an early stdin close still counts, a cancel propagates.</summary>
[Lane(TestLane.Integration)]
public sealed class ToolProcessTests : IDisposable
{
    private static readonly ToolSettings _settings = new("judge", "ui-evidence") { StopGrace = TimeSpan.FromSeconds(5) };

    private readonly StubTool _tool = new("trap 'echo stopped > \"$d/marker\"; exit 130' TERM\nsleep 30 &\nwait $!\n");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _tool.Dispose();

    [Fact]
    public async Task A_timeout_sends_SIGTERM_first_so_the_tool_can_clean_up_and_is_tool_unavailable()
    {
        var run = await ToolProcess.Run(Call(TimeSpan.FromMilliseconds(500)), _settings, Token);

        Assert.Equal(SindriErrors.ToolUnavailable.Code, run.Error.Code);
        Assert.Contains("didn't finish within", run.Error.Message, StringComparison.Ordinal);
        Assert.Equal("stopped", _tool.Read("marker").Trim());
    }

    [Fact]
    public async Task A_cancelled_call_stops_the_tool_and_throws()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(500));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ToolProcess.Run(Call(TimeSpan.FromSeconds(30)), _settings, cancel.Token));

        Assert.Equal("stopped", _tool.Read("marker").Trim());
    }

    [Fact]
    public async Task A_tool_that_closes_stdin_without_reading_it_still_reports_its_exit_and_output()
    {
        using var early = new StubTool("exec 0<&-\nprintf ok\nexit 3\n");

        var run = await ToolProcess.Run(new ToolCall("tool", early.Binary, [], new byte[1 << 20], TimeSpan.FromSeconds(10)), _settings, Token);

        Assert.Equal((3, "ok"), (run.Value.Exit, System.Text.Encoding.UTF8.GetString(run.Value.Stdout)));
    }

    [Fact]
    public async Task The_error_line_is_the_last_stderr_line_after_progress_lines()
    {
        using var noisy = new StubTool("echo 'ui-evidence: waiting on lock: /l' >&2\necho '{\"level\":\"warning\"}' >&2\necho 'ui-evidence: AW-EVIDENCE-009 busy' >&2\nexit 75\n");

        var run = await ToolProcess.Run(new ToolCall("ui-evidence", noisy.Binary, [], Stdin: null, TimeSpan.FromSeconds(10)), _settings, Token);

        Assert.Equal("ui-evidence: AW-EVIDENCE-009 busy", run.Value.Message);
    }

    private ToolCall Call(TimeSpan timeout) => new("tool", _tool.Binary, [], Stdin: null, timeout);
}
