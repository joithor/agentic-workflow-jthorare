using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>An accepted approval on a Home row (gh-1038): the shell reads the list again at once, so the approved row leaves Home without waiting for an event.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellRowApprovalTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> RunningShell()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _records.Write(A.Record());
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    [Fact]
    public Task An_accepted_recipe_approval_removes_the_row_once_the_list_is_read_again() => UiThread.Run(async () =>
    {
        var shell = await RunningShell();
        var row = NeedsYouRows.Recipe();
        shell.ShowNeedsYou(new NeedsYouFeed([row], ""));
        Assert.NotNull(shell.NeedsYou.Row(row.Id));

        shell.NeedsYou.Press(row.Id, row.Actions[0]);
        await ShellWait.Until(shell, () => shell.NeedsYou.Row(row.Id) is null, Token);

        _ = Assert.Single(_server.RecipeApprovals);
        Assert.Null(shell.NeedsYou.Row(row.Id));
    }, Token);

    [Fact]
    public Task A_refused_recipe_approval_keeps_the_row_with_its_reason() => UiThread.Run(async () =>
    {
        _server.ApproveRecipeAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Conflict, "SND-WF-009", "That version changed again.");
        var shell = await RunningShell();
        var row = NeedsYouRows.Recipe();
        shell.ShowNeedsYou(new NeedsYouFeed([row], ""));
        var reads = _server.NeedsYouCalls;

        shell.NeedsYou.Press(row.Id, row.Actions[0]);
        await ShellWait.Until(shell, () => shell.NeedsYou.Row(row.Id)?.ActionNote.Contains("SND-WF-009", StringComparison.Ordinal) == true, Token);

        Assert.Equal(reads, _server.NeedsYouCalls);
        Assert.NotNull(shell.NeedsYou.Row(row.Id));
    }, Token);
}
