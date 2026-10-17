namespace Aw.Sindri.Core.Onyx;

/// <summary>The API key Onyx returned once, and its id (which names the orphan to delete when storing it fails). Printing it never shows the key.</summary>
/// <param name="Key">The raw key; a secret.</param>
/// <param name="ApiKeyId">The key's id in Onyx.</param>
public sealed record OnyxMintedKey(string Key, int ApiKeyId)
{
    /// <summary>Describes the key without its value.</summary>
    /// <returns>The id only.</returns>
    public override string ToString() => $"OnyxMintedKey {{ ApiKeyId = {ApiKeyId} }}";
}
