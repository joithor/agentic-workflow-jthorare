using System.Globalization;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The words of 'context:index-sync': each record source's last sync, the same data as 'sindri index status'.</summary>
internal static class IndexSyncRules
{
    private const string Fix = "sindri index sync, then sindri index status";

    /// <summary>Warn for a source that never synced or whose last tick ended with a code; ok otherwise.</summary>
    /// <param name="rows">The feeds' progress.</param>
    /// <returns>The row.</returns>
    internal static DoctorCheck Judge(IReadOnlyList<SourceProgress> rows)
    {
        if (rows.Count == 0)
        {
            return new DoctorCheck(IndexDoctorChecks.SyncName, CheckState.Ok, "no record source feeds a repo yet", Fix: null);
        }

        var lines = rows.Select(Line).ToList();
        var problems = rows.Where(r => r.LastSync is null || r.LastCode is not null).Select(Line).ToList();
        return problems.Count > 0
            ? new DoctorCheck(IndexDoctorChecks.SyncName, CheckState.Warn, string.Join("; ", lines), Fix)
            : new DoctorCheck(IndexDoctorChecks.SyncName, CheckState.Ok, string.Join("; ", lines), Fix: null);
    }

    private static string Line(SourceProgress p) => p.LastSync is not { } at
        ? $"{p.Source.Value} never synced"
        : $"{p.Source.Value} synced {at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}Z{(p.LastCode is { } code ? $" ended {code}" : string.Empty)}";
}
