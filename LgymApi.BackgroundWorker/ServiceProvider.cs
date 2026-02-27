using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Notifications;
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
        }
        else
        {
            services.AddScoped<IEmailBackgroundScheduler, HangfireEmailBackgroundScheduler>();
        }

        services.AddScoped<IEmailScheduler<InvitationEmailPayload>, EmailSchedulerService<InvitationEmailPayload>>();
        services.AddScoped<IEmailScheduler<TrainingCompletedEmailPayload>, EmailSchedulerService<TrainingCompletedEmailPayload>>();
        services.AddScoped<IEmailScheduler<WelcomeEmailPayload>, EmailSchedulerService<WelcomeEmailPayload>>();
        services.AddScoped<IEmailJobHandler, EmailJobHandlerService>();

        services.AddScoped<IInvitationEmailJob, InvitationEmailJob>();
        services.AddScoped<IWelcomeEmailJob, WelcomeEmailJob>();
        services.AddScoped<IEmailJob, EmailJob>();

        services.AddScoped<InvitationEmailJob>();
        services.AddScoped<WelcomeEmailJob>();
        services.AddScoped<EmailJob>();

        return services;
    }
}
