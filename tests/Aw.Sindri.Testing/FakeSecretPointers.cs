using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>An ISecretPointers over a dictionary keyed by the pointer's text; an unknown pointer is SND-SECRET-002.</summary>
public sealed class FakeSecretPointers : ISecretPointers
{
    /// <summary>Gets the values by pointer text (`keychain:onyx/sindri-onyx`).</summary>
    public IDictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the pointers asked for, in order.</summary>
    public IList<string> Asked { get; } = [];

    /// <inheritdoc />
    public Result<string> Resolve(SecretPointer secretPointer)
    {
        ArgumentNullException.ThrowIfNull(secretPointer);
        var text = secretPointer.ToString();
        Asked.Add(text);
        return Values.TryGetValue(text, out var value) ? value : SindriErrors.SecretUnresolved.With($"The {text} secret doesn't resolve: nothing is stored there.");
    }
}
