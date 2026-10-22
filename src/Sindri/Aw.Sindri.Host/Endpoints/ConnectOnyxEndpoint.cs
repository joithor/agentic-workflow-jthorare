using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.ConnectOnyx;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/onyx/account (setup-once spec §5.6; desk): '{adminEmail, adminPassword}' wakes the Onyx setup job that waits for the login and answers the same job. The body is read by <see cref="OnyxLoginBody"/>, not bound; a login is kept only when
/// the job waits on 'onyx-account' (SND-SETUP-006 otherwise, with nothing kept), and it reaches the service through the request's hand-over, so the command carries only the job id.
/// </summary>
internal static class ConnectOnyxEndpoint
{
    internal static RouteGroupBuilder MapConnectOnyx(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/onyx/account", static async Task<IResult> (HttpContext http) =>
        {
            var services = http.RequestServices;
            var ct = http.RequestAborted;
            var login = await OnyxLoginBody.Read(http.Request.Body, ct).ConfigureAwait(false);
            if (login.IsError)
            {
                return login.ToHttpResult();
            }

            if (!services.GetRequiredService<IRunningJobs>().IsRunning(OnyxSetupJob.Kind, OnyxSetupJob.Key, out var job))
            {
                Result<JobStartedResponse> none = SindriErrors.SetupOnyxNotWaiting.With(SindriErrors.SetupOnyxNotWaiting.Title);
                return none.ToHttpResult();
            }

            var hand = services.GetRequiredService<IOnyxLoginHand>();
            hand.Give(login.Value);
            try
            {
                return (await services.GetRequiredService<IService<ConnectOnyxRequest, JobStartedResponse>>().Handle(new ConnectOnyxRequest(job), ct).ConfigureAwait(false)).ToHttpResult();
            }
            finally
            {
                _ = hand.Take();   // a refused call leaves nothing behind
            }
        });
        return group;
    }
}
