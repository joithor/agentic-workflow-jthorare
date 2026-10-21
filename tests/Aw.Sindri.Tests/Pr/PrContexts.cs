using System.Text.Json;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Pr;

/// <summary>Builds the contexts and the pull requests the babysitter's CI-red blocks are tested with.</summary>
internal static class PrContexts
{
    internal const string Link = "https://forge.test/acme/app/pull/7";
    internal const string Head = "aaaa1111";
    internal const string Tree = "/work/phase-1";

    /// <summary>A Step context holding the facts the way the block runner hands them over: each as the string element of its stored JSON text.</summary>
    internal static StepContext Step(string step, Dictionary<string, string> facts) => new(
        BlockId.From(step),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal),
        facts.ToDictionary(static f => FactKey.From("fact:" + f.Key), static f => JsonSerializer.SerializeToElement(f.Value)),
        InjectionSections.Empty);

    /// <summary>A gate context whose arguments are the given texts (a fact's stored text, or a literal).</summary>
    internal static GateContext Gate(string gate, Dictionary<string, string> arguments) => new(
        BlockId.From(gate),
        arguments.ToDictionary(static a => a.Key, static a => JsonSerializer.SerializeToElement(a.Value), StringComparer.Ordinal),
        new Dictionary<FactKey, JsonElement>(),
        Threshold: null);

    /// <summary>An open pull request whose checks <paramref name="state"/> ran on <paramref name="checksSha"/>.</summary>
    internal static PullView Pull(PullChecksState state = PullChecksState.Failing, string checksSha = Head, string head = Head, string[]? failing = null, int threads = 0, string? review = null, bool? mergeable = true) =>
        new(Link, PullState.Open, head, mergeable, review, threads, new PullChecks(state, checksSha, failing ?? (state == PullChecksState.Failing ? ["build"] : [])), MergedSha: null);

    /// <summary>The 'fact:prState' text the snapshot records for <paramref name="head"/> with <paramref name="failing"/> checks.</summary>
    internal static string State(string head = Head, params string[] failing) =>
        JsonSerializer.Serialize(new { pr = Link, headSha = head, checksSha = head, checks = "failing", failing });
}
