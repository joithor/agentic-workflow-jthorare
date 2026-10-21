using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.pr.autofixCap' (PR babysitter spec W27, §4.8): the CI fixes one pull request gets across instances. Default 3, at most 5.</summary>
public sealed class PrSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static SindriProfile With(int? cap) => _valid with { Workflow = new(Pr: new PrSettings(cap)) };

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, 3)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    public void The_cap_is_the_key_or_three(int? set, int expected)
    {
        var checkedProfile = ProfileRules.Check(With(set));

        Assert.True(checkedProfile.IsSuccess);
        Assert.Equal(expected, PrSettings.AutofixCapOf(checkedProfile.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_block_has_the_default_cap() =>
        Assert.Equal(PrSettings.DefaultAutofixCap, PrSettings.AutofixCapOf(ProfileRules.Check(_valid).Value));

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public void A_cap_outside_one_to_five_is_refused_naming_the_key(int cap)
    {
        var error = ProfileRules.Check(With(cap)).Error;

        Assert.Contains("workflow.pr.autofixCap", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_the_key_when_it_changes()
    {
        var live = ProfileRules.Check(_valid).Value;

        Assert.Equal(["workflow.pr.autofixCap"], ProfileDiff.Between(live, With(2)));
        Assert.Equal(["workflow.pr.autofixCap"], ProfileDiff.Between(With(2), With(4)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_block_keeps_its_canonical_json_and_hash()
    {
        var before = ProfileRules.Check(_valid with { Workflow = new("1m") }).Value;
        var json = AwJson.Serialize(before, SliceHarness.Json);

        Assert.DoesNotContain("\"pr\"", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
        Assert.NotEqual(ProfileHashing.Of(json), ProfileHashing.Of(AwJson.Serialize(before with { Workflow = new("1m", Pr: new PrSettings(2)) }, SliceHarness.Json)));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_the_block_and_refuses_an_unknown_pr_key()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  pr:\n    autofixCap: 4\n");
        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        Assert.Equal(4, PrSettings.AutofixCapOf(loaded.Value.Profile));

        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  pr:\n    autofixCap: 4\n    merge: yes\n");
        var refused = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
    }
}
