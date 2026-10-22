using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The inbox bar's confirmation panel in a real window (needs-you slice 3): the typed box and the hold reach the bar, the panel follows the bar, and A moves focus to the control without approving.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouConfirmViewTests : FrameHost
{
    private const string Phrase = "approve profile 70e158";

    [Fact]
    public Task The_typed_box_follows_the_bar_takes_focus_on_A_and_its_press_reaches_the_bar() => OnUi(async () =>
    {
        Server.AppChallengeAnswer = RouteAnswer.Api("workflow-challenges-app", "profile");
        File.AppendAllText("/tmp/ny3-marks.txt", "L1\n");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        File.AppendAllText("/tmp/ny3-marks.txt", "L2\n");
        shell.OpenNeedsYouInbox();
        File.AppendAllText("/tmp/ny3-marks.txt", "L3\n");
        shell.Inbox.Select("profile:drift");
        File.AppendAllText("/tmp/ny3-marks.txt", "L4\n");
        await Until(window, () => shell.Inbox.Bar.ShowsTyped);
        File.AppendAllText("/tmp/ny3-marks.txt", "L5\n");
        Settle(window);
        File.AppendAllText("/tmp/ny3-marks.txt", "L6\n");

        Assert.True(Visible(window, "NeedsYouConfirm") && Visible(window, "NeedsYouTypedRow"));
        File.AppendAllText("/tmp/ny3-marks.txt", "L7\n");
        Assert.False(Visible(window, "NeedsYouHoldRow"));
        File.AppendAllText("/tmp/ny3-marks.txt", "L8\n");

        await shell.Inbox.Bar.PressApprove();
        File.AppendAllText("/tmp/ny3-marks.txt", "L9\n");
        Settle(window);
        File.AppendAllText("/tmp/ny3-marks.txt", "L10\n");
        Assert.True(Named<TextBox>(window, "NeedsYouTyped").IsFocused);
        File.AppendAllText("/tmp/ny3-marks.txt", "L11\n");
        Assert.Empty(Server.ProfileApprovals);   // A approved nothing

        ((TypedConfirmBoxViewModel)Named<TypedConfirmBox>(window, "NeedsYouTypedBox").DataContext!).Text = Phrase;
        File.AppendAllText("/tmp/ny3-marks.txt", "L12\n");
        Named<Button>(window, "NeedsYouTypedButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        File.AppendAllText("/tmp/ny3-marks.txt", "L13\n");
        await Until(window, () => shell.Inbox.Bar.IsApproved);
        File.AppendAllText("/tmp/ny3-marks.txt", "L14\n");

        _ = Assert.Single(Server.ProfileApprovals);
        File.AppendAllText("/tmp/ny3-marks.txt", "L15\n");
        Assert.False(Visible(window, "NeedsYouConfirm"));
        File.AppendAllText("/tmp/ny3-marks.txt", "L16\n");
    });

    [Fact]
    public Task The_hold_follows_the_bar_and_a_complete_hold_reaches_the_bar() => OnUi(async () =>
    {
        Server.Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready");
        File.AppendAllText("/tmp/ny3-marks.txt", "L17\n");
        Server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-recipes");
        File.AppendAllText("/tmp/ny3-marks.txt", "L18\n");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        File.AppendAllText("/tmp/ny3-marks.txt", "L19\n");
        shell.OpenNeedsYouInbox();
        File.AppendAllText("/tmp/ny3-marks.txt", "L20\n");
        shell.Inbox.Select("setup:recipes");
        File.AppendAllText("/tmp/ny3-marks.txt", "L21\n");
        await Until(window, () => shell.Inbox.Bar.ShowsHold && shell.Inbox.Bar.HoldBox!.Gate is not null);
        File.AppendAllText("/tmp/ny3-marks.txt", "L22\n");
        Settle(window);
        File.AppendAllText("/tmp/ny3-marks.txt", "L23\n");

        Assert.True(Visible(window, "NeedsYouHoldRow"));
        File.AppendAllText("/tmp/ny3-marks.txt", "L24\n");
        Assert.False(Visible(window, "NeedsYouTypedRow"));
        File.AppendAllText("/tmp/ny3-marks.txt", "L25\n");

        await shell.Inbox.Bar.PressApprove();
        File.AppendAllText("/tmp/ny3-marks.txt", "L26\n");
        Settle(window);
        File.AppendAllText("/tmp/ny3-marks.txt", "L27\n");
        Assert.True(Named<Button>(window, "NeedsYouHold").IsFocused);
        File.AppendAllText("/tmp/ny3-marks.txt", "L28\n");
        Assert.Empty(Server.Approvals);
        File.AppendAllText("/tmp/ny3-marks.txt", "L29\n");

        var hold = shell.Inbox.Bar.HoldBox!;
        File.AppendAllText("/tmp/ny3-marks.txt", "L30\n");
        Assert.True(hold.Begin());
        File.AppendAllText("/tmp/ny3-marks.txt", "L31\n");
        Clock.Advance(TimeSpan.FromMilliseconds(hold.MinHoldMs));
        File.AppendAllText("/tmp/ny3-marks.txt", "L32\n");
        Named<HoldButton>(window, "NeedsYouHoldControl").Step();
        File.AppendAllText("/tmp/ny3-marks.txt", "L33\n");
        await Until(window, () => shell.Inbox.Bar.IsApproved);
        File.AppendAllText("/tmp/ny3-marks.txt", "L34\n");

        Assert.Equal(hold.MinHoldMs, Assert.Single(Server.Approvals).HeldMs);
        File.AppendAllText("/tmp/ny3-marks.txt", "L35\n");
    });
}
