using LgymApi.BackgroundWorker;
using LgymApi.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.TestUtils;

public static class TestServiceProviderFactory
{
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
