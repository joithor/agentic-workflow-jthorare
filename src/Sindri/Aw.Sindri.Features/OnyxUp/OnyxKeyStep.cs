using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxUp;

// Steps 7 and 8 (one step, so the key Onyx returns once is never held between two recorded steps): keep the stored key while Onyx still treats it as an admin
// service account; otherwise log the owner in (email asked, password read with echo off, both used once and dropped), create the key with the Admin group,
// check it answers as an admin service account, log out, store it. A failure after the key exists names it (name and id) so the owner can delete the orphan.
internal sealed class OnyxKeyStep(IOnyxProvisioning provisioning, ISecretWriter writer, ISecretPointers pointers, IOwnerConsole console, IOnyxProgress progress) : IOnyxUpStep
{
    private const string EmailPrompt = "Onyx owner email: ";
    private const string HiddenPrompt = "Onyx owner password: ";
    private const string NeedsTerminal = "run `sindri onyx up` in a terminal; it asks for the Onyx owner's email and password once, to create Sindri's key";

    public string Name => OnyxSteps.Key;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), Name, OnyxKeyPointers.Account);

    public async ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
    {
        var (kept, why) = await StoredKey(context, cancellationToken).ConfigureAwait(false);
        if (kept is not null)
        {
            context.KeyPointer = kept;
            return OnyxStepOutcome.Skipped;
        }

        if (!console.Interactive)
        {
            return Waiting(context);
        }

        progress.Line($"{Name}: {why}; creating one");
        var login = await Login(context, cancellationToken).ConfigureAwait(false);
        if (login.IsError)
        {
            return login.Error;
        }

        if (login.Value is not { } opened)
        {
            return Waiting(context);
        }

        using (opened)
        {
            return await MintAndStore(context, opened, cancellationToken).ConfigureAwait(false);
        }
    }

    // The first stored key Onyx accepts as an admin service account: the keychain's, then the fallback file's. One that can't be read is as good as none.
    private async ValueTask<(SecretPointer? Pointer, string Why)> StoredKey(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var rejected = false;
        foreach (var pointer in new[] { OnyxKeyPointers.Keychain, OnyxKeyPointers.FileFor(context.Paths.SindriHome) })
        {
            var stored = pointers.Resolve(pointer);
            if (stored.IsError)
            {
                continue;
            }

            var me = await provisioning.Me(context.Port, new ApiKeyCredential(stored.Value), cancellationToken).ConfigureAwait(false);
            if (me is { IsSuccess: true, Value: { IsAdmin: true, IsServiceAccount: true } })
            {
                return (pointer, string.Empty);
            }

            rejected = true;
        }

        return (null, rejected ? "the stored key isn't accepted" : "no stored key");
    }

    // The session, or null when the owner ended input or cancelled (nothing was sent). What was typed lives only inside this method.
    private async ValueTask<Result<OwnerSession?>> Login(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var email = console.ReadLine(EmailPrompt);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.Success<OwnerSession?>(value: null);
        }

        var hidden = console.ReadPassword(HiddenPrompt);
        if (string.IsNullOrEmpty(hidden))
        {
            return Results.Success<OwnerSession?>(value: null);
        }

        var login = await provisioning.Login(context.Port, email.Trim(), hidden, cancellationToken).ConfigureAwait(false);
        return login.IsError ? login.Error : Results.Success<OwnerSession?>(login.Value);
    }

    private async ValueTask<Result<OnyxStepOutcome>> MintAndStore(OnyxUpContext context, OwnerSession session, CancellationToken cancellationToken)
    {
        var minted = await provisioning.MintAdminKey(context.Port, session, OnyxKeyPointers.Account, cancellationToken).ConfigureAwait(false);
        var verified = minted.IsError ? minted : await Verify(context.Port, minted.Value, cancellationToken).ConfigureAwait(false);
        await LogOut(context.Port, session, cancellationToken).ConfigureAwait(false);
        if (verified.IsError)
        {
            return verified.Error;
        }

        var stored = writer.Store(verified.Value.Key);
        if (stored.IsError)
        {
            return SindriErrors.OnyxSecretWriteFailed.With($"{stored.Error.Message}; Onyx made the key {Describe(verified.Value)} and it wasn't stored: delete it on Onyx's API keys page, then run `sindri onyx up` again");
        }

        context.KeyPointer = stored.Value;
        progress.Line($"{Name}: stored Sindri's key as {stored.Value}");
        return OnyxStepOutcome.Ran;
    }

    // The key must answer as an admin service account before it is stored.
    private async ValueTask<Result<OnyxMintedKey>> Verify(int port, OnyxMintedKey minted, CancellationToken cancellationToken)
    {
        var me = await provisioning.Me(port, new ApiKeyCredential(minted.Key), cancellationToken).ConfigureAwait(false);
        return me is { IsSuccess: true, Value: { IsAdmin: true, IsServiceAccount: true } }
            ? minted
            : SindriErrors.OnyxKeyFailed.With($"Onyx made the key {Describe(minted)} but doesn't treat it as an admin service account: delete it on Onyx's API keys page; {OnyxFixes.KeyFailed}");
    }

    private async ValueTask LogOut(int port, OwnerSession session, CancellationToken cancellationToken)
    {
        var loggedOut = await provisioning.Logout(port, session, cancellationToken).ConfigureAwait(false);
        if (loggedOut.IsError)
        {
            progress.Line($"{Name}: couldn't log out of Onyx ({loggedOut.Error.Code}); the owner's session ends on its own");
        }
    }

    private static string Describe(OnyxMintedKey minted) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"'{OnyxKeyPointers.Account}' (id {minted.ApiKeyId})");

    private static Result<OnyxStepOutcome> Waiting(OnyxUpContext context)
    {
        context.Hint = NeedsTerminal;
        return OnyxStepOutcome.NeedsHuman;
    }
}
