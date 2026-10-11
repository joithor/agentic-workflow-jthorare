using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>Finds the evidence provider for a kind among the registered blocks (an evidence block that implements <see cref="IEvidenceProvider"/>).</summary>
/// <param name="blocks">Every registered block.</param>
public sealed class EvidenceProviders(IEnumerable<IBlock> blocks)
{
    private readonly IReadOnlyList<IEvidenceProvider> _providers = [.. blocks.OfType<IEvidenceProvider>()];

    /// <summary>The provider of <paramref name="kind"/> ('ui-browser' for block 'evidence.ui-browser').</summary>
    /// <param name="kind">The evidence kind.</param>
    /// <returns>The provider, or SND-BLK-003 naming the kinds that have one.</returns>
    public Result<IEvidenceProvider> Find(string kind)
    {
        var provider = BlockId.TryFrom($"evidence.{kind}", out var id) ? _providers.FirstOrDefault(p => p.Spec.Id == id) : null;
        return provider is null
            ? SindriErrors.ProviderUnknown.With($"no evidence provider for '{kind}' (providers: {string.Join(", ", Kinds())})")
            : Results.Success(provider);
    }

    private IEnumerable<string> Kinds() => _providers.Select(p => p.Spec.Id.Value["evidence.".Length..]).Order(StringComparer.Ordinal);
}
