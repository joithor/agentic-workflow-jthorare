using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The Linear record feed (spec 2026-10-27 sindri-owned index §5.2): the issues of the profile's teams, newest first because Linear's updatedAt order is newest first, from the committed watermark (<see cref="FeedCursor"/>)
/// or the window's start. A page costs one call; Linear's own end cursor pages inside a run, and the run's newest updatedAt becomes the watermark when it reaches the end.
/// </summary>
/// <param name="client">The Linear client.</param>
/// <param name="deadline">How long each call gets.</param>
internal sealed class LinearFeed(LinearClient client, TimeSpan deadline) : RecordFeed(SourceId.From("linear"))
{
    private const int MaxPage = 25;   // why: each issue brings its newest 20 comments and its relations, and Linear caps the cost of one query

    /// <inheritdoc />
    public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var at = FeedCursor.Read(pull.After);
        var variables = LinearFilters.Variables(Filter(at.Since, pull.NotBefore, pull.Before), Math.Clamp(pull.Limit, 1, MaxPage), at.After);
        var data = await client.Query<LinearIssuesData>("IssuesPull", variables, deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        var connection = data.Value.Issues;
        var items = connection.Nodes.Select(LinearIssueItem.Of).ToList();
        var top = items.Count == 0 ? at.Top : items.Max(static i => i.UpdatedAt) is { } newest && newest > (at.Top ?? DateTimeOffset.MinValue) ? newest : at.Top;
        var next = connection.PageInfo is { HasNextPage: true, EndCursor: { } end } ? new FeedCursor(at.Since, end, top) : new FeedCursor(top ?? at.Since, After: null, Top: null);
        return new RecordPage(items, [], next.ToCursor(), CursorReset: false);
    }

    private JsonObject Filter(DateTimeOffset? since, DateTimeOffset? notBefore, DateTimeOffset? before)
    {
        var filter = client.Settings.Teams.Count > 0 ? LinearFilters.InTeams([], client.Settings.Teams) : [];
        if (since is { } after && (notBefore is not { } window || window <= after))
        {
            filter["updatedAt"] = new JsonObject { ["gt"] = Iso(after) };
        }
        else if (notBefore is { } start)
        {
            filter["updatedAt"] = new JsonObject { ["gte"] = Iso(start) };
        }

        if (before is { } end)
        {
            var range = filter["updatedAt"] as JsonObject ?? [];
            range["lt"] = Iso(end);
            filter["updatedAt"] = range;
        }

        return filter;
    }

    private static string Iso(DateTimeOffset at) => at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
