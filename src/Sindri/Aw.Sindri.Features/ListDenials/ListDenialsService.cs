using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sandbox.Denials;

namespace Aw.Sindri.Features.ListDenials;

/// <summary>
/// Lists the run areas' denial counters (sandbox spec §3.6). '--since' keeps the entries last seen at or after it; a launch left with
/// nothing is dropped, and the overflow rows of a file stay only when the file itself was updated since.
/// </summary>
/// <param name="files">Where the counters are.</param>
/// <param name="owners">The item and repo of each launch.</param>
internal sealed class ListDenialsService(IDenialFiles files, ILaunchOwners owners) : IService<ListDenialsRequest, ListDenialsResponse>
{
    public async ValueTask<Result<ListDenialsResponse>> Handle(ListDenialsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = owners;
        var all = await files.List(request.Launch, cancellationToken).ConfigureAwait(false);
        if (request.Since is not { } since)
        {
            return new ListDenialsResponse(all);
        }

        var kept = all
            .Select(f => f with { Entries = [.. f.Entries.Where(e => e.Last >= since)], Overflow = f.UpdatedAt >= since ? f.Overflow : [] })
            .Where(static f => f.Entries.Count > 0 || f.Overflow.Count > 0);
        return new ListDenialsResponse([.. kept]);
    }
}
