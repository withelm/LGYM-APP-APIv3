using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Common.Outbox;
using LgymApi.Application.Outbox;
using LgymApi.BackgroundWorker.Notifications;
using LgymApi.BackgroundWorker.Outbox;
using LgymApi.Infrastructure.Jobs;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.BackgroundWorker;

public static class ServiceProvider
{
    public static IServiceCollection AddBackgroundWorkerServices(this IServiceCollection services, bool isTesting)
    {
        if (isTesting)
        {
            services.AddScoped<IEmailBackgroundScheduler, NoOpEmailBackgroundScheduler>();
            services.AddScoped<IOutboxDeliveryBackgroundScheduler, NoOpOutboxDeliveryBackgroundScheduler>();
        }
        else
        {
            services.AddScoped<IEmailBackgroundScheduler, HangfireEmailBackgroundScheduler>();
            services.AddScoped<IOutboxDeliveryBackgroundScheduler, HangfireOutboxDeliveryBackgroundScheduler>();
        }

        services.AddScoped<IEmailScheduler<InvitationEmailPayload>, EmailSchedulerService<InvitationEmailPayload>>();
        services.AddScoped<IEmailScheduler<TrainingCompletedEmailPayload>, EmailSchedulerService<TrainingCompletedEmailPayload>>();
        services.AddScoped<IEmailScheduler<WelcomeEmailPayload>, EmailSchedulerService<WelcomeEmailPayload>>();
        services.AddScoped<IEmailJobHandler, EmailJobHandlerService>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcherService>();
        services.AddScoped<IOutboxDeliveryProcessor, OutboxDeliveryProcessorService>();
        services.AddScoped<IOutboxDeliveryHandler, EmailNotificationOutboxDeliveryHandler>();

        services.AddScoped<IInvitationEmailJob, InvitationEmailJob>();
        services.AddScoped<IWelcomeEmailJob, WelcomeEmailJob>();
        services.AddScoped<IEmailJob, EmailJob>();
        services.AddScoped<IOutboxDispatcherJob, OutboxDispatcherJob>();
        services.AddScoped<IOutboxDeliveryJob, OutboxDeliveryJob>();

        services.AddScoped<InvitationEmailJob>();
        services.AddScoped<WelcomeEmailJob>();
        services.AddScoped<EmailJob>();
        services.AddScoped<OutboxDispatcherJob>();
        services.AddScoped<OutboxDeliveryJob>();

        return services;
    }
}
