using System.Text.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class IdJsonRoundTripTests
{
    private static readonly string _hash = new('a', 64);

    [Fact, Trait("Lane", "unit")]
    public void Ids_write_as_strings_and_read_back()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        Assert.Equal($"\"{_hash}\"", JsonSerializer.Serialize(ProfileHash.From(_hash), options));
        Assert.Equal(ServiceName.From("heartbeat"), JsonSerializer.Deserialize<ServiceName>("\"heartbeat\"", options));
        Assert.Equal(HostId.From("box"), JsonSerializer.Deserialize<HostId>("\"box\"", options));
        Assert.Equal(BootId.From("b-1"), JsonSerializer.Deserialize<BootId>("\"b-1\"", options));
    }

    [Fact, Trait("Lane", "unit")]
    public void Workflow_ids_write_as_strings_and_read_back()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        var sha = new string('b', 40);
        Assert.Equal("\"workflow.item\"", JsonSerializer.Serialize(RecipeId.From("workflow.item"), options));
        Assert.Equal(RecipeId.From("workflow.item"), JsonSerializer.Deserialize<RecipeId>("\"workflow.item\"", options));
        Assert.Equal(BlockId.From("gate.checks.passed"), JsonSerializer.Deserialize<BlockId>("\"gate.checks.passed\"", options));
        Assert.Equal(InstanceId.From("wf-0123456789ab"), JsonSerializer.Deserialize<InstanceId>("\"wf-0123456789ab\"", options));
        Assert.Equal(ItemId.From("ITEM-1"), JsonSerializer.Deserialize<ItemId>("\"ITEM-1\"", options));
        Assert.Equal(RecipeHash.From(_hash), JsonSerializer.Deserialize<RecipeHash>($"\"{_hash}\"", options));
        Assert.Equal(HeadSha.From(sha), JsonSerializer.Deserialize<HeadSha>($"\"{sha}\"", options));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"Workflow Item\"")]
    [InlineData("\"\"")]
    [InlineData("\"wf-XYZ\"")]
    public void A_malformed_workflow_id_is_a_json_error(string json)
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecipeId>(json, options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InstanceId>(json, options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<HeadSha>(json, options));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"ABC\"")]
    [InlineData("\"heart beat\"")]
    public void A_malformed_id_is_a_json_error(string json)
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProfileHash>(json, options));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ServiceName>(json, options));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_service_name_over_40_characters_is_refused() =>
        Assert.False(ServiceName.TryFrom(new string('a', 41), out _));
}
