using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInAppNotificationServiceDependencies, InAppNotificationServiceDependencies>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IStalePushInstallationCleanupService, StalePushInstallationCleanupService>();

        return services;
    }

    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddNotificationApplicationServices();
        services.AddScoped<INotificationEventBridge, NotificationEventBridge>();

        return services;
    }
}
