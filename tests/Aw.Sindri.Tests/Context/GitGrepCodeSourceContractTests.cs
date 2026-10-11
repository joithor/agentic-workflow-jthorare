using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Infrastructure.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

[Lane(TestLane.Integration)]
public sealed class GitGrepCodeSourceContractTests : ContextSourceContract, IDisposable
{
    private readonly TempTree _repo = new();

    public void Dispose() => _repo.Dispose();

    protected override Task<IContextSource> Create(IReadOnlyList<SeedDocument> documents)
    {
        foreach (var document in documents)
        {
            _ = _repo.Write($"src/{document.Name}.txt", document.Title + "\n" + document.Body + "\n");
        }

        _repo.GitAddAll();
        return Task.FromResult<IContextSource>(new GitGrepCodeSource([new CodeRepo("app", _repo.Path)]));
    }
}
