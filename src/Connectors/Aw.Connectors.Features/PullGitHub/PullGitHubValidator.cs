using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

internal sealed class PullGitHubValidator : IValidator<PullGitHubRequest>
{
    public Result<Unit> Validate(PullGitHubRequest value) => Unit.Value;
}
