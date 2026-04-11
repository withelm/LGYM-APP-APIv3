using LgymApi.BackgroundWorker;
using LgymApi.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.TestUtils;

/// <summary>
/// Builds pre-configured ServiceProvider instances for infrastructure and background worker testing.
/// </summary>
public static class TestServiceProviderFactory
{
    /// <summary>
    /// Creates a ServiceProvider with Infrastructure services and optional BackgroundWorker registration.
    /// </summary>
    public static Microsoft.Extensions.DependencyInjection.ServiceProvider CreateInfrastructureProvider(
        IConfiguration configuration,
        bool isTesting,
        bool includeBackgroundWorker = false,
        bool enableSensitiveLogging = false,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, enableSensitiveLogging, isTesting);

        if (includeBackgroundWorker)
        {
            services.AddBackgroundWorkerServices(isTesting);
        }

        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
