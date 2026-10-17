namespace Aw.Sindri.Core.Onyx;

/// <summary>What a request to Onyx proves who it is with: the owner's session cookie or an API key. Neither prints its secret.</summary>
public abstract class OnyxCredential
{
    private protected OnyxCredential()
    {
    }
}
