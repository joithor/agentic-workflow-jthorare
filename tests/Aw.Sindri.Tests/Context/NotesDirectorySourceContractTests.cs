using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Infrastructure.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

[Lane(TestLane.Integration)]
public sealed class NotesDirectorySourceContractTests : ContextSourceContract, IDisposable
{
    private readonly TempTree _notes = new();

    public void Dispose() => _notes.Dispose();

    protected override Task<IContextSource> Create(IReadOnlyList<SeedDocument> documents)
    {
        foreach (var document in documents)
        {
            _ = _notes.Write($"{document.Name}.md", "# " + document.Title + "\n" + document.Body + "\n");
        }

        _ = _notes.Write(".hidden/secret.md", "lease epoch fencing in a hidden folder");
        _ = _notes.Write("image.png", "lease epoch fencing in a non-note file");
        return Task.FromResult<IContextSource>(new NotesDirectorySource(_notes.Path));
    }
}
