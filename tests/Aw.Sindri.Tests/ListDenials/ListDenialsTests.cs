using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ListDenials;
using Aw.Sindri.Tests.Sandbox.Denials;

namespace Aw.Sindri.Tests.ListDenials;

/// <summary>'GET /sandbox/denials' (sandbox spec §3.6): the run areas' counters, optionally only what was last seen at or after a time.</summary>
[Lane(TestLane.Unit)]
public sealed class ListDenialsTests
{
    private static readonly DateTimeOffset _noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DenialEntry Seen(string target, DateTimeOffset last) => new(DenialKind.Network, target, DenialRules.NoGroup, 1, last, last);

    [Fact]
    public async Task Denials_since_filters_by_last_seen()
    {
        var old = Seen("old.example", _noon.AddHours(-3));
        var fresh = Seen("fresh.example", _noon.AddMinutes(-5));
        var stale = new DenialFile(LaunchId.From(DenialsRendererTests.LaunchTwo), _noon.AddHours(-3), [Seen("older.example", _noon.AddHours(-4))], [new DenialOverflow(DenialKind.Network, 9)]);
        var mixed = new DenialFile(LaunchId.From(DenialsRendererTests.LaunchOne), _noon, [old, fresh], [new DenialOverflow(DenialKind.Path, 2)]);
        var service = new ListDenialsService(new StubFiles(mixed, stale));

        var result = await service.Handle(new ListDenialsRequest(Launch: null, Since: _noon.AddHours(-1)), Token);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value.Launches);
        Assert.Equal([fresh], only.Entries);
        Assert.Equal([new DenialOverflow(DenialKind.Path, 2)], only.Overflow);   // the file was updated since, so its overflow stays
    }

    [Fact]
    public async Task Without_since_every_file_comes_back_whole_and_the_launch_filter_goes_to_the_port()
    {
        var launch = LaunchId.From(DenialsRendererTests.LaunchOne);
        var files = new StubFiles(new DenialFile(launch, _noon, [Seen("a.example", _noon.AddDays(-9))], []));

        var result = await new ListDenialsService(files).Handle(new ListDenialsRequest(launch), Token);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(Assert.Single(result.Value.Launches).Entries);
        Assert.Equal(launch, files.Asked);
    }

    private sealed class StubFiles(params DenialFile[] files) : IDenialFiles
    {
        public LaunchId? Asked { get; private set; }

        public ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken)
        {
            Asked = launch;
            return ValueTask.FromResult<IReadOnlyList<DenialFile>>(files);
        }
    }
}
