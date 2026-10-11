namespace Aw.Sindri.Infrastructure.Context;

/// <summary>The one HTTP client the Linear source uses (no proxy, a 10 s ceiling under the query's own deadline), disposed with the container.</summary>
internal sealed class LinearHttp : IDisposable
{
    public HttpClient Client { get; } = new(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };

    public void Dispose() => Client.Dispose();
}
