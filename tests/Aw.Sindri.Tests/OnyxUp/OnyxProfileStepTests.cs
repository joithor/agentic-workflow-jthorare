using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// Step 9: write context.sources.onyx; it uses the pointer the key step produced and never touches approval.
[Lane(TestLane.Unit)]
public sealed class OnyxProfileStepTests
{
    private readonly OnyxUpRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private OnyxProfileStep Step => new(_rig.Profile, _rig.Pins, _rig.Pointers, _rig.Progress);

    private OnyxUpContext Context(int port = 3002) => new(_rig.Pins.Pins, _rig.Paths, port, "linux/arm64");

    private async Task<Result<OnyxStepOutcome>> Run(OnyxUpContext context)
    {
        var step = Step;
        return await step.Execute(context, step.Stamp(context, "upstream"), recorded: null, upstreamRewrote: false, Token);
    }

    [Fact]
    public async Task It_writes_the_port_the_pinned_release_and_the_keychain_pointer_the_key_step_produced()
    {
        var context = Context();
        context.KeyPointer = OnyxKeyPointers.Keychain;

        var outcome = await Run(context);

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Equal(["3002 keychain:onyx/sindri-onyx v4.9.0"], _rig.Profile.Writes);
        Assert.Contains("url: \"http://127.0.0.1:3002\"", _rig.Profile.Text, StringComparison.Ordinal);
        Assert.Contains("version: \"v4.9.0\"", _rig.Profile.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_writes_the_file_pointer_when_that_is_where_the_key_went()
    {
        var context = Context();
        var file = OnyxKeyPointers.FileFor(_rig.Paths.SindriHome);
        context.KeyPointer = file;

        _ = await Run(context);

        Assert.Equal([$"3002 {file} v4.9.0"], _rig.Profile.Writes);
        Assert.Contains($"token: \"{file}\"", _rig.Profile.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_run_is_skipped_because_the_profile_already_holds_the_entry()
    {
        var context = Context();
        context.KeyPointer = OnyxKeyPointers.Keychain;
        _ = await Run(context);

        var second = await Run(context);

        Assert.Equal(OnyxStepOutcome.Skipped, second.Value);
    }

    [Fact]
    public async Task Without_a_pointer_from_the_key_step_it_resolves_the_stored_key_the_same_way()
    {
        _rig.Pointers.Values["keychain:onyx/sindri-onyx"] = "on_stored-key";

        var outcome = await Run(Context());

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Equal(["3002 keychain:onyx/sindri-onyx v4.9.0"], _rig.Profile.Writes);
    }

    [Fact]
    public async Task Without_any_stored_key_it_is_008_and_writes_nothing()
    {
        var outcome = await Run(Context());

        Assert.Equal("SND-ONYX-008", outcome.Error.Code);
        Assert.Empty(_rig.Profile.Writes);
    }

    [Fact]
    public async Task A_writer_error_is_the_steps_error_and_names_no_secret()
    {
        var context = Context();
        context.KeyPointer = OnyxKeyPointers.Keychain;
        _rig.Profile.Error = SindriErrors.ProfileInvalid.With("the profile has a flow-style context");

        var outcome = await Run(context);

        Assert.Equal("SND-PROFILE-002", outcome.Error.Code);
    }

    [Fact]
    public void The_stamp_follows_the_port_the_pointer_and_the_release()
    {
        var step = Step;
        var keychain = Context();
        keychain.KeyPointer = OnyxKeyPointers.Keychain;
        var file = Context();
        file.KeyPointer = OnyxKeyPointers.FileFor(_rig.Paths.SindriHome);

        Assert.NotEqual(step.Stamp(keychain, "a"), step.Stamp(file, "a"), StringComparer.Ordinal);
        Assert.NotEqual(step.Stamp(keychain, "a"), step.Stamp(Context(3004), "a"), StringComparer.Ordinal);
        Assert.Equal(step.Stamp(keychain, "a"), step.Stamp(keychain, "a"), StringComparer.Ordinal);
    }
}
