using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>The reply of `POST /api/admin/api-key` (APIKeyDescriptor): the raw key appears once, here.</summary>
/// <param name="ApiKeyId">The key's id.</param>
/// <param name="ApiKey">The raw key.</param>
internal sealed record KeyWire(
    [property: JsonPropertyName("api_key_id")] int ApiKeyId = 0,
    [property: JsonPropertyName("api_key")] string? ApiKey = null);
