using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.Views.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole map's "Map items" list from the keyboard (#1076): Enter on a focused row pins that node and opens its card.</summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapItemsKeyboardTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Enter_on_a_focused_map_item_pins_it_and_opens_its_card()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WholeMapItemsKeyboardTests).Assembly).Dispatch(() =>
        {
            var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
            map.Show(WholeMapLayoutTests.Draft("nodes-small"));
            var window = new Window { Content = new WholeMapView { DataContext = map }, Width = 1280, Height = 900 };
            try
            {
                window.Show();
                Settle(window);
                var list = window.GetVisualDescendants().OfType<ListBox>().First(l => string.Equals(l.Name, "WholeItems", StringComparison.Ordinal));
                var first = list.GetVisualDescendants().OfType<ListBoxItem>().First();
                _ = first.Focus(NavigationMethod.Tab);
                Settle(window);

                window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, keySymbol: null);
                Settle(window);
                var chosen = (MapItemViewModel)list.SelectedItem!;
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
                Settle(window);

                Assert.Equal(chosen.Key, map.Pinned);
                Assert.True(map.Panel.IsOpen);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }
}
