using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Pr;

/// <summary>Builds the context an effect of the CI-red loop is dispatched with: its facts stored, its arguments the stored references, as the engine resolves a '{ fact }' argument.</summary>
internal sealed class PrEffectRig(InMemoryFactStore facts)
{
    /// <summary>The context of <paramref name="effect"/> with the named arguments (a fact text, stored, or a literal after '='); the instance holds those facts too.</summary>
    internal EffectContext Context(string effect, string identity, IReadOnlyDictionary<string, string> stored, IReadOnlyDictionary<string, string>? literals = null)
    {
        Dictionary<string, JsonElement> arguments = new(StringComparer.Ordinal);
        Dictionary<FactKey, JsonElement> held = [];
        foreach (var (name, text) in stored)
        {
            var value = facts.Put(Encoding.UTF8.GetBytes(text), CancellationToken.None).AsTask().GetAwaiter().GetResult().Value;
            using var document = JsonDocument.Parse($$"""{"ref":"{{value.Ref}}","sha256":"{{value.Sha256}}","headSha":null}""");
            arguments[name] = document.RootElement.Clone();
            held[FactKey.From("fact:" + name)] = document.RootElement.Clone();
        }

        foreach (var (name, text) in literals ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            arguments[name] = JsonSerializer.SerializeToElement(text);
        }

        return new EffectContext(BlockId.From(effect), identity, "C-1", FromEngine: false, "T-3/workflow.pr-babysitter/1", "T-3", Attempt: 1, arguments, held, Text: null);
    }
}
