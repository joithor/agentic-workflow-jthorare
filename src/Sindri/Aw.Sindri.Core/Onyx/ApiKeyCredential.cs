namespace Aw.Sindri.Core.Onyx;

/// <summary>An Onyx API key, sent as `Authorization: Bearer`.</summary>
/// <param name="key">The raw key.</param>
public sealed class ApiKeyCredential(string key) : OnyxCredential
{
    /// <summary>Gets the raw key.</summary>
    public string Key { get; } = key;

    /// <summary>Describes the credential without its value.</summary>
    /// <returns>A fixed text.</returns>
    public override string ToString() => "ApiKeyCredential";
}
