using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<IInAppNotificationServiceDependencies, InAppNotificationServiceDependencies>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        return services;
    }
}
