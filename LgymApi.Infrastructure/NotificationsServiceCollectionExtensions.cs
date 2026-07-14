using LgymApi.Application.Notifications;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Push;
using LgymApi.Infrastructure.Configuration;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isTesting)
    {
        services.AddNotificationsModule();
        services.AddNotificationsInfrastructure(configuration, isTesting);

        return services;
    }

    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isTesting)
    {
        var pushNotificationOptions = PushNotificationOptionsFactory.Create(configuration);

        PushNotificationOptionsFactory.Validate(pushNotificationOptions);

        services.AddSingleton(pushNotificationOptions);
        services.AddSingleton<IStalePushInstallationCleanupSettings, PushInstallationCleanupSettings>();
        services.AddScoped<IPushProviderSender, FcmPushSender>();

        if (isTesting)
        {
            services.AddScoped<IPushBackgroundScheduler, NoOpPushBackgroundScheduler>();
        }
        else
        {
            services.AddScoped<IPushBackgroundScheduler, HangfirePushBackgroundScheduler>();
        }

        services.AddScoped<IPushInstallationRepository, PushInstallationRepository>();
        services.AddScoped<IPushNotificationMessageRepository, PushNotificationMessageRepository>();
        services.AddScoped<IInAppNotificationRepository, InAppNotificationRepository>();

        return services;
    }
}
