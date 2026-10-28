using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Shell;

// The pill's problem text gives way before the ⌘K field: it gets only what is left once the field has room for its
// whole placeholder (#1544), never more than its own 220 px and never less than nothing. The rest of the step-down
// (fold the field, drop the age, ...) is Choose's, over the narrower pill.
internal sealed partial class TopBar
{
    private const double ProblemsMax = 220;

    private void CapProblems(double width)
    {
        if (double.IsInfinity(width) || Content is not Border bar)
        {
            PillProblems.MaxWidth = ProblemsMax;
            return;
        }

        double search;
        if (_fit == Fit.Full)
        {
            SearchField.Measure(Size.Infinity);
            search = Math.Clamp(SearchField.DesiredSize.Width, SearchMin, SearchMax);
        }
        else
        {
            search = _compactSearch;
        }

        var problems = PillProblems.IsVisible ? PillProblems.DesiredSize.Width + PillProblems.Margin.Left + PillProblems.Margin.Right : 0;
        var others = bar.Padding.Left + bar.Padding.Right + Brand.DesiredSize.Width
            + NavigationWidthWithCounts + search + PillGroup.DesiredSize.Width - problems;
        PillProblems.MaxWidth = Math.Clamp(width - others - PillProblems.Margin.Left - PillProblems.Margin.Right, 0, ProblemsMax);
    }
}
