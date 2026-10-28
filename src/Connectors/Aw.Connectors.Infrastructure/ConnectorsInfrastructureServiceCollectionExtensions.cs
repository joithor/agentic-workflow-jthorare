using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Infrastructure;

/// <summary>Registers Aw.Connectors.Infrastructure's adapters and settings. A second technology for a port gets its own AddConnectors&lt;Tech&gt;Infrastructure method.</summary>
public static class ConnectorsInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Connectors adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddConnectorsInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
