using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Tests.Workflow.Ship;

/// <summary>An <see cref="IBriefFiles"/> that holds files by directory and name, so a phase packet and a proof pack can both have an acceptance.json.</summary>
internal sealed class DirectoryFiles : IBriefFiles
{
    private readonly Dictionary<(string Dir, string Name), string> _files = [];

    /// <summary>Holds a file.</summary>
    /// <param name="dir">The directory.</param>
    /// <param name="name">The file's name.</param>
    /// <param name="text">The text.</param>
    /// <returns>The same instance.</returns>
    public DirectoryFiles With(string dir, string name, string text)
    {
        _files[(dir, name)] = text;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<BriefFile>> Read(string path, IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roots);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(roots.Count == 1 && _files.TryGetValue((roots[0], path), out var text)
            ? Results.Success(new BriefFile(path, text, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12]))
            : SindriErrors.InvalidInput.With($"the file {path} does not exist under {string.Join(',', roots)}"));
    }
}
