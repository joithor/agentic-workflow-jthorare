using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxOpen;

/// <summary>
/// Names the Onyx page to open, from state.json alone (no network, no password): the sign-up page until the `signup` step is recorded, the connectors page after. Onyx v4.9.0 has no
/// unauthenticated user-count route that was verified, so "Onyx has no account yet" is what `onyx up` recorded. Without a state file Onyx isn't set up.
/// </summary>
/// <param name="workspace">Reads state.json.</param>
internal sealed class OnyxOpenService(IOnyxWorkspace workspace) : IService<OnyxOpenRequest, OnyxOpenResponse>
{
    public ValueTask<Result<OnyxOpenResponse>> Handle(OnyxOpenRequest request, CancellationToken cancellationToken)
    {
        if (workspace.ReadState() is not { } state)
        {
            return ValueTask.FromResult<Result<OnyxOpenResponse>>(SindriErrors.InvalidInput.With("Onyx isn't set up; run `sindri onyx up`"));
        }

        var signedUp = state.Steps.ContainsKey(OnyxSteps.Signup);
        return ValueTask.FromResult<Result<OnyxOpenResponse>>(signedUp
            ? new OnyxOpenResponse(OnyxPages.Connectors(state.Port), "connectors")
            : new OnyxOpenResponse(OnyxPages.SignUp(state.Port), "sign-up"));
    }
}
