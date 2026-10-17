using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'context.sources.onyx' as 'profile validate' and doctor 'context:onyx' judge it, before any call to Onyx.</summary>
public sealed class OnyxProfileTests
{
    private static readonly PointerSecrets _noSecrets = new(_ => null, new FakeToolRunner(ToolRun.Missing));
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_refuses_an_onyx_key_written_into_the_profile_without_repeating_it()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"on_live-key-0123","version":"v4.9.0"}}}""");
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, ProfileContextChecks.Default);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.Equal("SND-CONTEXT-002", validated.Error.Code);
        Assert.StartsWith("context.sources.onyx.token:", validated.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("on_live-key-0123", validated.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_reports_an_onyx_url_off_loopback_with_its_key_path()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://onyx.example:3000","token":"env:ONYX_KEY","version":"v4.9.0"}}}""");
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, ProfileContextChecks.Default);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.Equal("SND-CONTEXT-055", validated.Error.Code);
        Assert.StartsWith("context.sources.onyx.url:", validated.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_skips_onyx_as_ok_when_the_approved_profile_names_none()
    {
        var approved = SliceHarness.Profile();
        _ = await _h.Profiles.Save(approved, Token);

        var check = await new OnyxDoctor(FakeContextEngine.Instance, _noSecrets).Check(approved, Token);

        Assert.Equal(new DoctorCheck("context:onyx", CheckState.Ok, "not configured (the approved profile names no context.sources.onyx)", Fix: null), check);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_skips_onyx_as_ok_when_no_profile_is_approved()
    {
        var check = await new OnyxDoctor(FakeContextEngine.Instance, _noSecrets).Check(approved: null, Token);

        Assert.Equal((CheckState.Ok, "not configured (no profile is approved)"), (check.State, check.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_warns_when_onyx_is_named_but_no_repo_is_mapped_so_no_source_runs()
    {
        var approved = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"env:ONYX_KEY","version":"v4.9.0"}}}""");

        var check = await new OnyxDoctor(FakeContextEngine.Instance, _noSecrets).Check(approved, Token);

        Assert.Equal((CheckState.Warn, "the approved profile maps no repo, so no context source runs, onyx included"), (check.State, check.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_fails_with_the_pointers_SND_SECRET_reason_before_any_call_to_onyx()
    {
        var approved = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"env:ONYX_KEY","version":"v4.9.0"}}}""", [new RepoEntry("split-panes", "/repos/split-panes")]);

        var check = await new OnyxDoctor(FakeContextEngine.Instance, _noSecrets).Check(approved, Token);

        Assert.Equal(CheckState.Fail, check.State);
        Assert.Equal("SND-SECRET-002: The env:ONYX_KEY secret doesn't resolve: the variable is unset or empty; set it before starting Sindri.", check.Detail);
        Assert.Contains("docs/sindri/onyx.md", check.Fix, StringComparison.Ordinal);   // the test engine's Sources would answer 011: it was never asked
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_refuses_a_managed_source_pinned_to_another_release_naming_the_pins_release()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"keychain:onyx/sindri-onyx","version":"v4.8.0","managed":true}}}""");
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, ProfileContextChecks.Default);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.Equal("SND-CONTEXT-055", validated.Error.Code);
        Assert.StartsWith("context.sources.onyx.version:", validated.Error.Message, StringComparison.Ordinal);
        Assert.Contains("v4.9.0", validated.Error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri onyx up", validated.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("v4.9.0")]
    [InlineData("4.9.0")]
    public async Task Validate_accepts_a_managed_source_at_the_pinned_release_with_or_without_the_v(string version)
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"keychain:onyx/sindri-onyx","version":"VERSION","managed":true}}}""".Replace("VERSION", version, StringComparison.Ordinal));
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, ProfileContextChecks.Default);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.True(validated.IsSuccess, validated.IsError ? validated.Error.Message : "");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_leaves_an_unmanaged_source_at_another_release_alone()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"env:ONYX_KEY","version":"v4.8.0"}}}""");
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, ProfileContextChecks.Default);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.True(validated.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approve_refuses_the_same_managed_source()
    {
        _h.Profiles.Live = With("""{"sources":{"onyx":{"url":"http://127.0.0.1:3000","token":"keychain:onyx/sindri-onyx","version":"v4.8.0","managed":true}}}""");
        var service = new Features.ApproveProfile.ApproveProfileService(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events, _h.Challenges, ProfileContextChecks.Default);

        var preview = await service.Handle(new Features.ApproveProfile.ApproveProfileRequest(Hash: null), Token);

        Assert.Equal("SND-CONTEXT-055", preview.Error.Code);
    }

    private static LoadedProfile With(string context, IReadOnlyList<RepoEntry>? repos = null)
    {
        var json = SliceHarness.Json;
        var profile = new SindriProfile(1, "thor", Repos: repos, Context: JsonDocument.Parse(context).RootElement.Clone());
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, json)), "/home/profile.yaml");
    }
}
