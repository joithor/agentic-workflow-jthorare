using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>Home's Needs-you card and the setup line (setup-once spec 5.7, #1207): the card hides a setup row only for the step the line names, so every other setup row (Approve all workflows) stays where a click can reach it.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSetupHomeRowsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new()
    {
        Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready"),
        NeedsYouView = RouteAnswer.Api("needs-you", "empty"),
        SetupAnswer = SetupFixture.Answer("extra-priority-mix"),   // repo done, profile failed: the line names the profile
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouEntry Row(string step) => NeedsYouRows.SetupRecipes() with { Id = "setup:" + step };

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Setup.IsHomeLineShown, Token);
        return shell;
    }

    [Fact]
    public Task Home_keeps_a_setup_row_the_setup_line_does_not_name() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        shell.ShowNeedsYou(new NeedsYouFeed([Row("recipes")], ""));

        Assert.Equal(["setup:recipes"], shell.NeedsYou.Rows.Select(r => r.Id), StringComparer.Ordinal);   // the line names the profile, not the workflows
    }, Token);

    [Fact]
    public Task Home_hides_the_setup_row_of_the_step_the_line_names_and_keeps_the_others() => UiThread.Run(async () =>
    {
        _server.SetupAnswer = new RouteAnswer(HttpStatusCode.OK, SetupFixture.With("extra-priority-mix", "profile", "done").ToJsonString());   // now the first open step is the workflows
        var shell = await Connected();

        shell.ShowNeedsYou(new NeedsYouFeed([Row("recipes"), Row("mcp")], ""));

        Assert.Equal(["setup:mcp"], shell.NeedsYou.Rows.Select(r => r.Id), StringComparer.Ordinal);
    }, Token);
}
