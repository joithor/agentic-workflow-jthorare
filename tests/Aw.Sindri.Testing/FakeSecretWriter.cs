using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>An ISecretWriter that keeps the value in memory and answers a scripted pointer or error. Held to <see cref="SecretWriterContract"/>.</summary>
public sealed class FakeSecretWriter : ISecretWriter
{
    /// <summary>Gets or sets the pointer a store returns.</summary>
    public SecretPointer Answer { get; set; } = OnyxKeyPointers.Keychain;

    /// <summary>Gets or sets an error every store returns instead (nothing is kept).</summary>
    public Error? Error { get; set; }

    /// <summary>Gets the values stored, in order.</summary>
    public IList<string> Stored { get; } = [];

    /// <inheritdoc />
    public Result<SecretPointer> Store(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Error is { } error)
        {
            return error;
        }

        if (value.Length == 0)
        {
            return SindriErrors.OnyxSecretWriteFailed.With("there is no key to store; " + OnyxFixes.SecretWriteFailed);
        }

        Stored.Add(value);
        return Answer;
    }
}
