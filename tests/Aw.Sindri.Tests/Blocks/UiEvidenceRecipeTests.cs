using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.CollectEvidence;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Blocks;

/// <summary>The built-in workflow.ui-evidence: an evidence check, an evidence provider and a judge gate in one approved recipe.</summary>
[Lane(TestLane.Unit)]
public sealed class UiEvidenceRecipeTests
{
    private static readonly HeadSha _sha = HeadSha.From(new string('f', 40));

    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact]
    public void The_builtin_validates_approved_and_is_refused_unapproved_for_its_judge_gate()
    {
        var (recipe, hash) = TestRecipes.Read(Registries.BuiltinYaml("workflow.ui-evidence"));

        var approved = RecipeValidator.Validate(recipe, hash, approved: true, Registries.Builtin(), new HashSet<RecipeId>());
        var unapproved = RecipeValidator.Validate(recipe, hash, approved: false, Registries.Builtin(), new HashSet<RecipeId>());

        Assert.Empty(approved);
        var problem = Assert.Single(unapproved);
        Assert.Equal((RecipeChecks.Capability, "transitions[2].gates[1]"), (problem.Check, problem.Path));
        Assert.Contains("gate.judge.visual-critique", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unhealthy_stack_refuses_ready_and_writes_nothing()
    {
        await using var h = await Started();
        var i = h.Instance;
        h.Harness.UiEvidence.Health = new StackHealth(Healthy: false, ["the database is not ready"]);

        var refused = await h.Harness.Signal(i, "ready");

        Assert.Equal(SindriErrors.GateRefused.Code, refused.Error.Code);
        Assert.Contains("ui-evidence doctor: the database is not ready", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal((i.State, i.Version), ((await h.Harness.Get(i.Id)).State, (await h.Harness.Get(i.Id)).Version));
    }

    [Fact]
    public async Task Doctor_collect_provenance_and_judge_take_the_instance_to_done()
    {
        await using var h = await Started();
        var harness = h.Harness;
        _ = harness.Judge.Answer("visual-critique", new JudgeAnswer("model-decided", "looks-right", 0.93, "claude-cli", JudgeDecisionId.From("d-7")));
        _ = (await harness.Signal(h.Instance, "ready")).Value;
        _ = (await new CollectEvidenceService(harness.Providers, harness.Store, harness.Engine).Handle(Collect(h.Instance.Id), Token)).Value;

        var done = (await harness.Signal(h.Instance.Id, Ran())).Value.Instance;

        Assert.Equal(("done", InstanceStatus.Done), (done.State, done.Status));
        Assert.Equal(new VisualCritiqueInput("desktop/1.png", "/runs/r1"), Assert.Single(harness.Judge.Asked));
        Assert.Equal(["doctor", "provenance"], harness.UiEvidence.Calls.Where(c => !c.StartsWith("run ", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_unsure_judge_refuses_ran_and_the_instance_stays_in_run()
    {
        await using var h = await Started();
        var harness = h.Harness;
        _ = (await harness.Signal(h.Instance, "ready")).Value;
        _ = (await new CollectEvidenceService(harness.Providers, harness.Store, harness.Engine).Handle(Collect(h.Instance.Id), Token)).Value;
        var before = await harness.Get(h.Instance.Id);

        var refused = await harness.Signal(h.Instance.Id, Ran());

        Assert.Equal(SindriErrors.GateRefused.Code, refused.Error.Code);
        Assert.Contains("judge visual-critique: escalated (no-provider-decided)", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(("run", before.Version), ((await harness.Get(h.Instance.Id)).State, (await harness.Get(h.Instance.Id)).Version));
    }

    [Fact]
    public async Task Ran_without_collected_evidence_is_refused_even_when_judge_passes()
    {
        await using var h = await Started();
        _ = h.Harness.Judge.Answer("visual-critique", new JudgeAnswer("model-decided", "looks-right", 0.93, "claude-cli", JudgeDecisionId.From("d-8")));
        var ready = (await h.Harness.Signal(h.Instance, "ready")).Value.Instance;

        var refused = await h.Harness.Signal(h.Instance.Id, Ran());

        Assert.Equal(SindriErrors.EvidenceMissing.Code, refused.Error.Code);
        Assert.Equal(("run", ready.Version), ((await h.Harness.Get(h.Instance.Id)).State, (await h.Harness.Get(h.Instance.Id)).Version));
    }

    private static WorkflowSignal Ran() => new("ran", ExpectedVersion: null, _sha,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["visual.evidence-dir"] = "/runs/r1", ["visual.screenshot"] = "desktop/1.png" }, [], EmptyDiff: false, Spend.Zero);

    private static CollectEvidenceRequest Collect(InstanceId id) =>
        new(id, "ui-browser", _sha, new Dictionary<string, string>(StringComparer.Ordinal) { ["script"] = "/s.json", ["run-dir"] = "/runs/r1", ["host"] = "http://localhost:3000" });

    private static async Task<StartedRecipe> Started()
    {
        var harness = new WorkflowHarness();
        harness.Add(Registries.BuiltinYaml("workflow.ui-evidence"));
        _ = await harness.HoldLease();
        return new StartedRecipe(harness, await harness.Start("workflow.ui-evidence"));
    }

    private sealed record StartedRecipe(WorkflowHarness Harness, WorkflowInstance Instance) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }
}
