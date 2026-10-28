using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Issue 1468: a run whose report was refused twice (the second ends its session, so the watch reads 'refused') is not completed from the output file the agent left on disk, the very
/// output that was refused. It ends as refused, and the model pick's one retry on a stronger model follows.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class RefusedRecoveryTests : AgentRuntimeTestBase
{
    private static readonly ModelFacts[] _three =
    [
        new("haiku", "Haiku", ModelSource.Catalog, 0),
        new("sonnet", "Sonnet", ModelSource.Catalog, 1),
        new("opus", "Opus", ModelSource.Catalog, 2),
    ];

    [Fact(Timeout = 120_000)]
    public async Task A_refused_run_with_its_output_left_on_disk_is_retried_not_recovered()
    {
        var ct = TestContext.Current.CancellationToken;
        UsePick();
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _diff.Add(rig.Worktree + "-e1");
            _outputs.Add(rig.Worktree, "resolution", "the output that was refused");
            rig.Sindri.GetRequiredService<ReportRefusals>().Record(rig.Launch, "fact:resolution is not valid JSON");   // what CompleteStep does before launcher.Stop
            _sessions.End(rig.Launch, SessionEndReason.Exit, exitCode: 0);

            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            var running = await WorkflowsHarness.Until(handle, v => v.AgentRun?.Launch == rig.EscalatedLaunch.Value);

            Assert.Equal([rig.Launch, rig.EscalatedLaunch], _launcher.Requests.Select(static r => r.Launch));
            Assert.Equal("write", running.State);   // not 'done': the refused output was not accepted
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_that_ended_without_a_refusal_is_still_recovered_from_disk()
    {
        var ct = TestContext.Current.CancellationToken;
        UsePick();
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _outputs.Add(rig.Worktree, "resolution", "The button handler was missing.");
            _sessions.End(rig.Launch, SessionEndReason.Exit, exitCode: 0);

            var done = await WorkflowsHarness.Until(handle, static v => v.State == "done");

            Assert.Equal("done", done.State);
            _ = Assert.Single(_launcher.Requests);
        });
    }

    private void UsePick()
    {
        var judge = new FakeModelPickJudge().Chooses("m1");
        _extra = services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickJudge>(judge));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickSettings>(new FakeModelPickSettings { Settings = new ModelPickSettings(PickOn: true) }));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickHistory>(new FakeModelPickHistory()));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelCatalog>(new FakeModelCatalog().With(AgentId.From("claude-code"), _three)));
        };
    }
}
