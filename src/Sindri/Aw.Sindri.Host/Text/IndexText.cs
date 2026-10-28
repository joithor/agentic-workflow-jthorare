using System.Globalization;
using System.Text;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>The text of 'sindri index status' (owned-index spec §5.8): one line per source, the state as a word, the last tick and the code it ended with.</summary>
internal static class IndexText
{
    internal static string Status(IndexStatusView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Sources.Count == 0)
        {
            return "no record source is set up (fix: add one to the profile's context section)\n";
        }

        var width = view.Sources.Max(static s => s.Source.Length);
        var text = new StringBuilder();
        foreach (var row in view.Sources)
        {
            var sync = row.LastSync is { } at ? "last sync " + at.ToString("u", CultureInfo.InvariantCulture) : "no sync yet";
            _ = text.Append(row.Source.PadRight(width)).Append("  ").Append(State(row)).Append(" · ").Append(sync);
            _ = row.LastCode is { } code ? text.Append(" · ").Append(code) : text;
            _ = text.Append('\n');
        }

        return text.ToString();
    }

    private static string State(IndexSourceRow row) =>
        row.Complete ? "complete"
        : row.Usable ? "usable, filling older history"
        : row.Phase is null ? "not started"
        : "syncing recent window";

    internal static string Counts(IndexPurgeView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return string.Create(CultureInfo.InvariantCulture, $"{view.Records:N0} records, {view.Chunks:N0} chunks, {view.Vectors:N0} vectors");
    }
}
