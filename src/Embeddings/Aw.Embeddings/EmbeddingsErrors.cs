using Aw.Kernel;

namespace Aw.Embeddings;

/// <summary>The Embeddings error catalogue (codes AW-EMBEDDINGS-NNN).</summary>
public static class EmbeddingsErrors
{
    /// <summary>AW-EMBEDDINGS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-EMBEDDINGS-001", ErrorKind.Invalid, "Embeddings input failed validation");

    /// <summary>Every Embeddings error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
