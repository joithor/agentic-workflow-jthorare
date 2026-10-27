using System.Globalization;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ListDenials;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/sandbox/denials?launch=&amp;since= (sandbox spec §3.6): the denial counters, grouped for the owner by the CLI and the app.</summary>
internal static class ListDenialsEndpoint
{
    internal static RouteGroupBuilder MapListDenials(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sandbox/denials", static async (string? launch, string? since, IService<ListDenialsRequest, ListDenialsResponse> service, CancellationToken ct) =>
        {
            LaunchId? id = null;
            if (!string.IsNullOrEmpty(launch))
            {
                var parsed = LaunchRoute.Launch(launch);
                if (parsed.IsError)
                {
                    return parsed.ToHttpResult();
                }

                id = parsed.Value;
            }

            DateTimeOffset? from = null;
            if (!string.IsNullOrEmpty(since))
            {
                if (!DateTimeOffset.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
                {
                    return HttpResults.Problem(SindriErrors.InvalidInput.With($"since must be a time such as 2026-10-27T09:00:00Z, not '{since}'"));
                }

                from = at;
            }

            return (await service.Handle(new ListDenialsRequest(id, from), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
