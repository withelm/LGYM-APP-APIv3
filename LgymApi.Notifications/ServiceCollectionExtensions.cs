using LgymApi.Notifications.Application;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<IInAppNotificationServiceDependencies, InAppNotificationServiceDependencies>();
        services.AddScoped<InAppNotificationService>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        return services;
    }
}
