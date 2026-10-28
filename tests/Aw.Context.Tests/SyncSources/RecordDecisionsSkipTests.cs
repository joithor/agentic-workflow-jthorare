using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SyncSources;

/// <summary>A source the intent walk already reads keeps its decisions from that walk: its record feed gets no decision pass of its own (#1373).</summary>
[Trait("Lane", "unit")]
public sealed class RecordDecisionsSkipTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_feed_whose_source_also_has_an_intent_walk_is_not_read_for_decisions_twice()
    {
        var now = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);   // MapWorld's clock
        var issue = new RecordItem(
            RecordKey.From("github:issue/acme/shop/7"), RecordKind.Issue, "Pane resize limits", "Pane resize: we agreed the minimum pane width is 20 columns.", Author: null, Url: null, RecordState.Open, StateRaw: null,
            CreatedAt: null, now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("repo:acme/shop"));
        var walk = new FakeContextSource(new SourceDescriptor(RecordSamples.Github, SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false), []);
        using var world = new MapWorld(intent: walk, feeds: new FakeRecordFeedCatalog(new FakeRecordFeed(RecordSamples.Github, [issue])));
        _ = await world.ImportFixture();

        var report = await world.Engine.Sync(source: null, MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}"), Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        _ = Assert.Single((await world.Records.List(new RecordFilter(), after: null, 10, Ct)).Value);   // the record did sync
        Assert.DoesNotContain((await world.Intents.Layer(Ct)).Value.Records, r => r.Key == issue.Key);
        Assert.Empty(world.Model.Requests);
    }
}
