using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.ShowWorkflow;

namespace Aw.Sindri.Host.Commands;

/// <summary>The workflow verbs' text: state and status are always words, never colour.</summary>
internal static class WorkflowText
{
    internal static string Line(WorkflowInstance i) => string.Create(CultureInfo.InvariantCulture,
        $"{i.Id.Value}  {i.Recipe.Value,-14} {i.Item.Value}#{i.Attempt}  {i.State,-10} {WorkflowWords.Status(i)}  v{i.Version}");

    internal static string List(IReadOnlyList<WorkflowInstance> instances) =>
        instances.Count == 0 ? "no workflow instances\n" : string.Join('\n', instances.Select(Line)) + "\n";

    internal static string Outcome(WorkflowOutcome o)
    {
        var text = new StringBuilder(Line(o.Instance));
        _ = text.Append(o.AlreadyRecorded ? "  (already recorded; nothing changed)\n" : "\n");
        foreach (var g in o.Gates)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"  gate {g.Gate.Value} ({Rung(g)}): {(g.Passed ? "passed" : "failed")} - {g.Detail}\n");
        }

        if (o.Instance.Hold is { } hold)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"  {WorkflowWords.Status(o.Instance)}: {hold.Detail}\n");
        }

        return text.ToString();
    }

    internal static string Show(ShowWorkflowResponse r)
    {
        var i = r.Instance;
        var text = new StringBuilder();
        Row(text, "instance", i.Id.Value);
        Row(text, "recipe", $"{i.Recipe.Value} (hash {i.RecipeHash.Abbreviated})");
        Row(text, "item", string.Create(CultureInfo.InvariantCulture, $"{i.Item.Value}, attempt {i.Attempt}{(i.Parent is { } p ? $", parent {p.Value}" : string.Empty)}"));
        Row(text, "state", string.Create(CultureInfo.InvariantCulture, $"{i.State} ({Kind(r.Kind)}), version {i.Version}"));
        Row(text, "status", r.Status + (i.Hold is { } h ? $": {h.Detail}" : string.Empty));
        Row(text, "timer", Timer(i));
        Row(text, "loops", i.Loops.Count == 0 ? "none" : string.Join(", ", i.Loops.Select(l => string.Create(CultureInfo.InvariantCulture, $"{l.Key} {l.Value}"))));
        Row(text, "spent", string.Create(CultureInfo.InvariantCulture, $"tokens {i.Spent.Tokens}, turns {i.Spent.Turns}"));
        Row(text, "facts", i.Facts.Count == 0 ? "none" : string.Join(", ", i.Facts.Select(f => $"{f.Key}={f.Value}")));
        Row(text, "evidence", i.Evidence.Count == 0 ? "none" : string.Join(", ", i.Evidence.Select(e => $"{e.Kind}@{e.Sha.Value}")));
        Row(text, "signals", r.Signals.Count == 0 ? "none" : string.Join(", ", r.Signals));
        Row(text, "next", r.Next ?? "-");
        foreach (var child in r.Children)
        {
            Row(text, "child", Line(child));
        }

        Row(text, "history", string.Create(CultureInfo.InvariantCulture, $"{r.History.Count} events"));
        foreach (var e in r.History)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"  {e.Sequence,5} {e.Type,-28} epoch {e.Epoch}  {e.Key}\n");
        }

        return text.ToString();
    }

    internal static string Recipes(IReadOnlyList<CatalogEntry> recipes) => string.Join('\n', recipes.Select(r =>
        $"{r.Id?.Value ?? "?",-16} v{r.Version?.ToString(CultureInfo.InvariantCulture) ?? "?"}  {(r.Valid ? "valid" : "invalid")}  {(r.Approved ? "approved" : "unapproved")}  {r.Hash?.Abbreviated ?? "-"}  {r.Source}"
        + string.Concat(r.Problems.Select(p => $"\n  {p}")))) + "\n";

    internal static string Validation(CatalogEntry entry) => entry.Valid
        ? $"valid: {entry.Id?.Value} v{entry.Version?.ToString(CultureInfo.InvariantCulture)}, hash {entry.Hash?.Value}\n"
        : string.Create(CultureInfo.InvariantCulture, $"invalid: {entry.Source} ({entry.Problems.Count} problems)\n") + string.Concat(entry.Problems.Select(p => $"  {p}\n"));

    private static void Row(StringBuilder text, string name, string value) => text.Append(CultureInfo.InvariantCulture, $"{name,-9} {value}\n");

    private static string Kind(StateKind? kind) => kind switch
    {
        StateKind.Active => "active",
        StateKind.Wait => "wait",
        StateKind.Terminal => "terminal",
        _ => "?",
    };

    private static string Rung(GateRecord g) => g.Rung.ToString().ToLowerInvariant();

    private static string Timer(WorkflowInstance i) => (i.Deadline, i.Paused) switch
    {
        ({ } d, _) => d.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        (null, { } left) => "paused, " + Left(left) + " left",
        _ => "none",
    };

    /// <summary>A duration in its two largest whole units, for example '8m 44s' or '2d 3h'.</summary>
    private static string Left(TimeSpan left)
    {
        var parts = new[] { (left.Days, "d"), (left.Hours, "h"), (left.Minutes, "m"), (left.Seconds, "s") }
            .SkipWhile(p => p.Item1 == 0).Take(2).Where(p => p.Item1 > 0)
            .Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Item1}{p.Item2}")).ToList();
        return parts.Count == 0 ? "under 1s" : string.Join(' ', parts);
    }
}
