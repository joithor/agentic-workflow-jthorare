using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>What every <see cref="IOnyxProvisioning"/> honours: a login that works or says SND-ONYX-008, an admin key that is an admin service account, SND-ONYX-007 for an owner who isn't an admin, and no secret in a description.</summary>
public abstract class ProvisioningContract
{
    /// <summary>Gets the name the key is minted under.</summary>
    protected const string KeyName = "sindri-onyx";

    /// <summary>A rig over an Onyx whose owner is (or isn't) an admin.</summary>
    /// <param name="ownerIsAdmin">Whether the owner is an admin member of the `Admin` group.</param>
    /// <returns>The rig.</returns>
    protected abstract ProvisioningRig Create(bool ownerIsAdmin);

    /// <summary>A good login returns a session whose description holds no secret.</summary>
    [Fact]
    public async Task A_good_login_returns_a_session_that_does_not_print_its_cookie()
    {
        using var rig = Create(ownerIsAdmin: true);

        var login = await rig.Client.Login(rig.Port, rig.Email, rig.Password, TestContext.Current.CancellationToken);

        Expect.True(login.IsSuccess);
        using var session = login.Value;
        Expect.Equal("OwnerSession", session.ToString());
    }

    /// <summary>A wrong password is SND-ONYX-008, and the error never repeats the password.</summary>
    [Fact]
    public async Task A_wrong_password_is_008_and_the_error_does_not_repeat_it()
    {
        using var rig = Create(ownerIsAdmin: true);
        const string wrong = "definitely-the-wrong-password";

        var login = await rig.Client.Login(rig.Port, rig.Email, wrong, TestContext.Current.CancellationToken);

        Expect.Equal("SND-ONYX-008", login.Error.Code);
        Expect.True(!login.Error.Message.Contains(wrong, StringComparison.Ordinal));
        Expect.True(!login.Error.Message.Contains(rig.Password, StringComparison.Ordinal));
    }

    /// <summary>An admin owner can mint a key, and the key is an admin service account.</summary>
    [Fact]
    public async Task An_admin_owner_mints_a_key_that_is_an_admin_service_account()
    {
        using var rig = Create(ownerIsAdmin: true);
        using var session = (await rig.Client.Login(rig.Port, rig.Email, rig.Password, TestContext.Current.CancellationToken)).Value;

        var minted = await rig.Client.MintAdminKey(rig.Port, session, KeyName, TestContext.Current.CancellationToken);

        Expect.True(minted.IsSuccess);
        Expect.True(minted.Value.Key.Length > 0);
        Expect.True(!minted.Value.ToString().Contains(minted.Value.Key, StringComparison.Ordinal));
        var key = await rig.Client.Me(rig.Port, new ApiKeyCredential(minted.Value.Key), TestContext.Current.CancellationToken);
        Expect.Equal(new OnyxAccount(key.Value.Email, IsAdmin: true, IsServiceAccount: true), key.Value);
        var owner = await rig.Client.Me(rig.Port, session, TestContext.Current.CancellationToken);
        Expect.Equal(new OnyxAccount(rig.Email, IsAdmin: true, IsServiceAccount: false), owner.Value);
    }

    /// <summary>An owner who isn't an admin gets SND-ONYX-007 and no key.</summary>
    [Fact]
    public async Task An_owner_who_is_not_an_admin_gets_007()
    {
        using var rig = Create(ownerIsAdmin: false);
        using var session = (await rig.Client.Login(rig.Port, rig.Email, rig.Password, TestContext.Current.CancellationToken)).Value;

        var minted = await rig.Client.MintAdminKey(rig.Port, session, KeyName, TestContext.Current.CancellationToken);

        Expect.Equal("SND-ONYX-007", minted.Error.Code);
    }

    /// <summary>A key Onyx doesn't know is refused as SND-ONYX-008.</summary>
    [Fact]
    public async Task An_unknown_key_is_refused_008()
    {
        using var rig = Create(ownerIsAdmin: true);

        var me = await rig.Client.Me(rig.Port, new ApiKeyCredential("on_not-a-key"), TestContext.Current.CancellationToken);

        Expect.Equal("SND-ONYX-008", me.Error.Code);
    }

    /// <summary>A logout of a live session succeeds.</summary>
    [Fact]
    public async Task A_logout_of_a_live_session_succeeds()
    {
        using var rig = Create(ownerIsAdmin: true);
        using var session = (await rig.Client.Login(rig.Port, rig.Email, rig.Password, TestContext.Current.CancellationToken)).Value;

        var loggedOut = await rig.Client.Logout(rig.Port, session, TestContext.Current.CancellationToken);

        Expect.True(loggedOut.IsSuccess);
    }
}
