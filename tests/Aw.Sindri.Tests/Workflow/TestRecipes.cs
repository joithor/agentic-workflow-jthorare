using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Infrastructure.Recipes;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Recipes for engine tests, read from YAML through the real reader.</summary>
internal static class TestRecipes
{
    /// <summary>A loop with a checks gate, a model-rung gate on 'risky', an effect, short timeouts and a budget.</summary>
    internal static readonly string Loop = string.Join(
        '\n',
        "id: workflow.loop",
        "version: 1",
        "initial: work",
        "states:",
        "  work: { kind: active, step: step.checks, timeout: 10m, onTimeout: retry }",
        "  review: { kind: wait, timeout: 1h, onTimeout: escalate }",
        "  done: { kind: terminal }",
        "transitions:",
        "  - { from: work, to: review, on: passed, gates: [gate.checks.passed] }",
        "  - { from: work, to: work, on: failed, loop: fix }",
        "  - { from: review, to: done, on: approved, effects: [effect.notify.human] }",
        "  - { from: review, to: done, on: risky, gates: [gate.test.model] }",
        "  - { from: review, to: done, on: tested, evidence: [test@head] }",
        "loops: { fix: { cap: 2 } }",
        "budget: { tokens: 1000, turns: 10 }");

    /// <summary>A parent whose 'wait' state runs workflow.loop as a child.</summary>
    internal static readonly string Parent = string.Join(
        '\n',
        "id: workflow.parent",
        "version: 1",
        "initial: prepare",
        "states:",
        "  prepare: { kind: wait, timeout: 1h }",
        "  running: { kind: wait, child: workflow.loop, timeout: 1d }",
        "  done: { kind: terminal }",
        "transitions:",
        "  - { from: prepare, to: running, on: go }",
        "  - { from: running, to: done, on: child.done }");

    /// <summary>A grandparent whose 'running' state runs workflow.parent, which runs workflow.loop: a tree two levels deep.</summary>
    internal static readonly string Grand = string.Join(
        '\n',
        "id: workflow.grand",
        "version: 1",
        "initial: prepare",
        "states:",
        "  prepare: { kind: wait, timeout: 1h }",
        "  running: { kind: wait, child: workflow.parent, timeout: 2d }",
        "  done: { kind: terminal }",
        "transitions:",
        "  - { from: prepare, to: running, on: go }",
        "  - { from: running, to: done, on: child.done }");

    internal static (Recipe Recipe, RecipeHash Hash) Read(string yaml)
    {
        var read = RecipeReading.Read(yaml, SliceHarness.Json);
        return read is { Recipe: { } r, Hash: { } h } ? (r, h) : throw new InvalidOperationException(read.Problem?.ToString());
    }
}
