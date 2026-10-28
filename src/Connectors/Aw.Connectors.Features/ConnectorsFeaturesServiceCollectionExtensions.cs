using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Features.PullGitHub;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Features;

/// <summary>Registers Aw.Connectors.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class ConnectorsFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Connectors use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddConnectorsFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IValidator<PullGitHubRequest>, PullGitHubValidator>()
            .AddScoped<IService<PullGitHubRequest, PullGitHubResponse>>(static sp =>
                new ValidatingService<PullGitHubRequest, PullGitHubResponse>(
                    new PullGitHubService(sp.GetRequiredService<IGitHubReader>()),   // a factory registration: the reader is the caller's to add, so a host that builds readers per token registers none
                    sp.GetRequiredService<IValidator<PullGitHubRequest>>()));
        _ = services.AddSingleton<Aw.Connectors.Core.GitHub.IGitHubConnectorFactory>(static sp => new PullGitHub.GitHubConnectorFactory(sp.GetService<TimeProvider>() ?? TimeProvider.System));
        return services;
    }
}
