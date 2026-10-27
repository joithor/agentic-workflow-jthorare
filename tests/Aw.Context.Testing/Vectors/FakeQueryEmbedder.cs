using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Testing.Vectors;

/// <summary>An <see cref="IQueryEmbedder"/> that hashes words into a small bag-of-words vector, so texts that share words are close; it passes <see cref="QueryEmbedderContract{TEmbedder}"/>.</summary>
public sealed class FakeQueryEmbedder : IQueryEmbedder
{
    /// <summary>The dimension of every vector.</summary>
    public const int Dim = 16;

    /// <summary>The model name every vector carries.</summary>
    public const string Model = "fake-embedder@1";

    /// <summary>When set, every call fails with it (the model is down, say).</summary>
    public Error? Failure { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<QueryEmbedding>> Embed(string text, CancellationToken cancellationToken) => throw new NotSupportedException();
}
