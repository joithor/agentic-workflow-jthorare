using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Recipes;

/// <summary>Every recipe check of factory addendum §17.28.5, red and green.</summary>
public sealed class RecipeValidatorTests
{
    private static readonly string _valid = string.Join(
        '\n',
        "id: workflow.sample",
        "version: 1",
        "initial: triaged",
        "states:",
        "  triaged: { kind: active, step: step.triage, timeout: 1h }",
        "  ready: { kind: wait, timeout: 1d, onTimeout: escalate }",
        "  checks: { kind: active, step: step.checks, timeout: 10m, onTimeout: retry }",
        "  done: { kind: terminal }",
        "transitions:",
        "  - { from: triaged, to: ready, on: triaged, gates: [gate.triage.complete] }",
        "  - { from: ready, to: checks, on: start, gates: [gate.router.startable] }",
        "  - { from: checks, to: done, on: passed, gates: [gate.checks.passed], effects: [effect.notify.human], evidence: [test@head] }",
        "  - { from: checks, to: checks, on: failed, loop: fix }",
        "loops: { fix: { cap: 3 } }");

    public static TheoryData<string, string, string> Broken => new()
    {
        { "step.triage", "step.nothing", RecipeChecks.Block },
        { "gates: [gate.router.startable]", "gates: [step.claim]", RecipeChecks.Block },
        { "ready: { kind: wait, timeout: 1d, onTimeout: escalate }", "ready: { kind: wait, timeout: 1d, onTimeout: escalate }\n  lost: { kind: wait, timeout: 1h }\n", RecipeChecks.Reachable },
        { "ready: { kind: wait, timeout: 1d, onTimeout: escalate }", "ready: { kind: wait, onTimeout: escalate }", RecipeChecks.Timeout },
        { "loops: { fix: { cap: 3 } }", "loops: { fix: { cap: 0 } }", RecipeChecks.Loop },
        { "on: failed, loop: fix }", "on: failed }", RecipeChecks.Loop },
        { "effects: [effect.notify.human]", "effects: [effect.test.mail]", RecipeChecks.Effect },
        { "evidence: [test@head]", "evidence: [video@head]", RecipeChecks.Block },
        { "initial: triaged", "initial: ready", RecipeChecks.Input },
        { "- { from: checks, to: done", "- { from: done, to: checks, on: reopen }\n  - { from: checks, to: done", RecipeChecks.Shape },
    };

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_wired_correctly_has_no_problems() => Assert.Empty(Problems(_valid));

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Broken))]
    public void Each_check_reports_what_breaks_it(string from, string to, string check)
    {
        var problems = Problems(_valid.Replace(from, to, StringComparison.Ordinal));

        Assert.Contains(problems, p => p.Check == check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_non_terminal_state_without_a_way_out_is_reported()
    {
        var problems = Problems(_valid.Replace("- { from: checks, to: checks, on: failed, loop: fix }", "- { from: checks, to: ready, on: back }", StringComparison.Ordinal)
            .Replace("- { from: checks, to: done, on: passed, gates: [gate.checks.passed], effects: [effect.notify.human], evidence: [test@head] }", "- { from: ready, to: ready, on: wait, loop: fix }", StringComparison.Ordinal));

        Assert.Contains(problems, p => string.Equals(p.Check, RecipeChecks.Exit, StringComparison.Ordinal) || string.Equals(p.Check, RecipeChecks.Reachable, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capability_block_is_refused_in_an_unapproved_recipe_only()
    {
        var yaml = _valid.Replace("effects: [effect.notify.human]", "effects: [effect.pr.draft]", StringComparison.Ordinal);

        Assert.Empty(Problems(yaml, approved: true));
        Assert.Contains(Problems(yaml, approved: false), p => p.Check == RecipeChecks.Capability && p.Path == "transitions[2].effects[0]");
    }

    [Fact, Trait("Lane", "unit")]
    public void An_input_produced_with_another_type_is_named()
    {
        var problems = Problems(_valid.Replace("gates: [gate.checks.passed]", "gates: [gate.test.size]", StringComparison.Ordinal), extraSizeGate: true);

        Assert.Contains(problems, p => p.Check == RecipeChecks.Input && p.Message.Contains("produced as triage.size:size", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_frozen_hash_that_does_not_match_the_content_is_reported()
    {
        var (recipe, hash) = TestRecipes.Read(_valid);
        var frozen = recipe with { Hash = RecipeHash.From(new string('0', 64)) };

        var problems = RecipeValidator.Validate(frozen, hash, approved: true, Registries.Builtin(), new HashSet<RecipeId>());

        Assert.Equal(RecipeChecks.Hash, Assert.Single(problems).Check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_must_be_registered_and_waited_for()
    {
        var yaml = _valid.Replace("ready: { kind: wait, timeout: 1d, onTimeout: escalate }", "ready: { kind: wait, timeout: 1d, child: workflow.other }", StringComparison.Ordinal);

        var problems = Problems(yaml);

        Assert.Contains(problems, p => p.Path == "states.ready.child" && p.Message.Contains("isn't registered", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Path == "states.ready.child" && p.Message.Contains("child.done", StringComparison.Ordinal));
    }

    private static IReadOnlyList<RecipeProblem> Problems(string yaml, bool approved = false, bool extraSizeGate = false)
    {
        var (recipe, hash) = TestRecipes.Read(yaml);
        var blocks = extraSizeGate ? Registries.Builtin(new SizeAsTextGate(), new MailEffect()) : Registries.Builtin(new MailEffect());
        return RecipeValidator.Validate(recipe, hash, approved, blocks, new HashSet<RecipeId> { recipe.Id });
    }
}
