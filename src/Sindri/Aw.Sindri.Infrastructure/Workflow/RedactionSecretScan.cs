using Aw.Redaction;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The secret scan over the one pattern set (Aw.Redaction).</summary>
/// <param name="redactor">The redactor; the built-in rules when none is given.</param>
internal sealed class RedactionSecretScan(IRedactor? redactor = null) : ISecretScan
{
    public IReadOnlyList<int> Hits(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _ = redactor;
        throw new NotSupportedException();
    }
}
