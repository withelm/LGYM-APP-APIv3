using LgymApi.Application.Notifications;
using LgymApi.Application.Options;
using LgymApi.Notifications;
using LgymApi.Notifications.Persistence;
using LgymApi.Application.Notifications.Authorization;
using LgymApi.Infrastructure.Repositories.Notifications;
using LgymApi.Application.Notifications.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IEmailNotificationLeaseSettings>(static provider =>
            new EmailNotificationLeaseSettings(
                provider.GetRequiredService<BackgroundCommandOptions>().EmailSendLeaseSeconds));
        services.AddScoped<INotificationSourceFactPort, NotificationSourceFactProvider>();
        services.AddScoped<IPushInstallationPersistenceConflictClassifier, NpgsqlPushInstallationPersistenceConflictClassifier>();

        return services;
    }
}
