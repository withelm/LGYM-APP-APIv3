using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInAppNotificationServiceDependencies, InAppNotificationServiceDependencies>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IPushNotificationDeliveryServiceDependencies, PushNotificationDeliveryServiceDependencies>();
        services.AddScoped<IPushNotificationDeliveryService, PushNotificationDeliveryService>();
        services.AddScoped<IStalePushInstallationCleanupService, StalePushInstallationCleanupService>();
        services.AddScoped<IPushInstallationLifecycleService, PushInstallationLifecycleService>();
        services.AddScoped<IPushInstallationSessionDisassociationService, PushInstallationLifecycleService>();

        return services;
    }

    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddNotificationApplicationServices();
        services.AddScoped<INotificationEventBridge, NotificationEventBridge>();

        return services;
    }
}
