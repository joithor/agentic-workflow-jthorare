using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The version list's read. A store not yet upgraded (reads never migrate) answers with no ruleset on any version before 0012, no dropped counts before 0013 and no structure reason before 0016.</summary>
internal static class GraphVersionReads
{
    public static async ValueTask<Result<IReadOnlyList<MapVersion>>> List(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var ruleset = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasRulesetColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        var dropped = ruleset.IsError ? ruleset : await SqliteQueries.One(session, new SqlStatement(GraphSql.HasDroppedColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        var checks = dropped.IsError ? dropped : await SqliteQueries.One(session, new SqlStatement(GraphSql.HasChecksColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        if (checks.IsError)
        {
            return checks.Error;
        }

        var select = checks.Value > 0 ? GraphSql.SelectVersions : dropped.Value > 0 ? GraphSql.SelectVersionsBefore0016 : ruleset.Value > 0 ? GraphSql.SelectVersionsBefore0013 : GraphSql.SelectVersionsBefore0012;
        return await SqliteQueries.List(session, new SqlStatement(select), GraphRows.Version, cancellationToken).ConfigureAwait(false);
    }
}
