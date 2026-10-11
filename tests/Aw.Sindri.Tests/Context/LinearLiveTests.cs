using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Context;

namespace Aw.Sindri.Tests.Context;

/// <summary>The real Linear API, only with a key in the environment; otherwise skipped with the reason. Nothing is recorded.</summary>
public sealed class LinearLiveTests
{
    [Fact, Trait("Lane", "heavy")]
    public async Task A_live_search_returns_well_formed_hits()
    {
        var key = Environment.GetEnvironmentVariable("LINEAR_API_KEY");
        Assert.SkipWhen(string.IsNullOrEmpty(key), "LINEAR_API_KEY is not set: the live Linear check needs a read-only key in the environment");
        using var linear = new LinearHttp();
        var source = new LinearSource(linear.Client, LinearSource.Api, key!, new LinearContext(), SliceHarness.Json);   // why: SkipWhen returned, so key is set

        var hits = await source.Search(new SourceQuery(["the"], 3), TestContext.Current.CancellationToken);

        Assert.True(hits.IsSuccess, hits.IsError ? hits.Error.Message : string.Empty);
        Assert.All(hits.Value, h => Assert.StartsWith("https://linear.app/", h.Document, StringComparison.Ordinal));
    }
}
