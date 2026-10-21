using Aw.Kernel;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Host.Forge;

/// <summary>The pull requests of a Sindri with no forge wired.</summary>
internal sealed class NoForgePullRequests : IPullRequests
{
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken) => throw new NotSupportedException();
}
