using System.Globalization;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>An issue as the record feed hands it over: its text is the description, the newest 20 comments (where decisions live), the project and the relations, as <see cref="LinearText"/> joins them; Linear's state type normalised and its own word kept, the closing time, the team as the audience, and the parent and relations as links.</summary>
internal static class LinearIssueItem
{
    private const string Prefix = "linear:issue/";

    /// <summary>The item for an issue.</summary>
    /// <param name="issue">The issue.</param>
    /// <returns>The item.</returns>
    public static RecordItem Of(LinearIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new RecordItem(
            RecordKey.From(Prefix + issue.Id), RecordKind.Issue, issue.Title, LinearText.Of(issue), issue.Creator?.Name, issue.Url, State(issue.State?.Type), issue.State?.Name,
            issue.CreatedAt, issue.UpdatedAt, issue.CompletedAt ?? issue.CanceledAt, SourceVersion.From(issue.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
            Links(issue), Repo: null, issue.Team is { } team && AudienceKey.TryFrom("linear:" + team.Id, out var audience) ? audience : null);
    }

    private static RecordState? State(string? type) => type switch
    {
        "completed" => RecordState.Done,
        "canceled" => RecordState.Canceled,
        "triage" or "backlog" or "unstarted" or "started" => RecordState.Open,
        _ => null,
    };

    private static List<RecordLink> Links(LinearIssue issue)
    {
        var links = new List<RecordLink>();
        if (issue.Parent is { } parent)
        {
            links.Add(Link(parent, RecordLinkKind.Parent));
        }

        links.AddRange(Outgoing(issue));
        links.AddRange(Incoming(issue));
        return [.. links.DistinctBy(static l => (l.ToRef, l.Kind))];
    }

    private static IEnumerable<RecordLink> Outgoing(LinearIssue issue) =>
        (issue.Relations?.Nodes ?? []).Where(static r => r.RelatedIssue is not null && RelationKind(r.Type) is not null).Select(static r => Link(r.RelatedIssue!, RelationKind(r.Type)!.Value));

    // why: an inverse 'blocks' or 'duplicate' says the other issue blocks or duplicates this one, which the link kinds (this record blocks the other) have no word for; only 'related' reads the same from both sides
    private static IEnumerable<RecordLink> Incoming(LinearIssue issue) =>
        (issue.InverseRelations?.Nodes ?? []).Where(static r => r.Issue is not null && string.Equals(r.Type, "related", StringComparison.Ordinal)).Select(static r => Link(r.Issue!, RecordLinkKind.Relates));

    private static RecordLinkKind? RelationKind(string type) => type switch
    {
        "blocks" => RecordLinkKind.Blocks,
        "duplicate" => RecordLinkKind.Duplicates,
        "related" => RecordLinkKind.Relates,
        _ => null,
    };

    private static RecordLink Link(LinearIssueRef other, RecordLinkKind kind) => new(Prefix + (other.Id ?? other.Identifier), kind, other.Title);
}
