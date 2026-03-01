using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Jobs;
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

        if (isTesting)
        {
            services.AddScoped<IActionMessageScheduler, NoOpActionMessageScheduler>();
        }
        else
        {
            services.AddScoped<IActionMessageScheduler, HangfireActionMessageScheduler>();
        }

        services.AddScoped<IEmailScheduler<InvitationEmailPayload>, EmailSchedulerService<InvitationEmailPayload>>();
        services.AddScoped<IEmailScheduler<TrainingCompletedEmailPayload>, EmailSchedulerService<TrainingCompletedEmailPayload>>();
        services.AddScoped<IEmailScheduler<WelcomeEmailPayload>, EmailSchedulerService<WelcomeEmailPayload>>();
        services.AddScoped<IEmailJobHandler, EmailJobHandlerService>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        services.AddScoped<IInvitationEmailJob, InvitationEmailJob>();
        services.AddScoped<IWelcomeEmailJob, WelcomeEmailJob>();
        services.AddScoped<IEmailJob, EmailJob>();

        services.AddScoped<InvitationEmailJob>();
        services.AddScoped<WelcomeEmailJob>();
        services.AddScoped<EmailJob>();
        services.AddScoped<IActionMessageJob, ActionMessageJob>();
        services.AddScoped<ActionMessageJob>();

        services.AddScoped<BackgroundActionOrchestratorService>();

        // Register typed background action handlers
        services.AddBackgroundAction<UserRegisteredCommand, SendRegistrationEmailHandler>();
        services.AddBackgroundAction<InvitationCreatedCommand, SendInvitationEmailHandler>();
        services.AddBackgroundAction<TrainingCompletedCommand, TrainingCompletedEmailCommandHandler>();
        services.AddBackgroundAction<TrainingCompletedCommand, UpdateTrainingMainRecordsHandler>();


        return services;
    }

    /// <summary>
    /// Registers a strongly-typed background action handler for a specific command type.
    /// Handlers are resolved at runtime by exact command type only (1:1 matching).
    /// Multiple handlers can be registered for the same command type.
    /// </summary>
    /// <typeparam name="TCommand">The command type this action handles. Must implement IActionCommand.</typeparam>
    /// <typeparam name="TAction">The action handler implementation. Must implement IBackgroundAction&lt;TCommand&gt;.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddBackgroundAction<TCommand, TAction>(this IServiceCollection services)
        where TCommand : IActionCommand
        where TAction : class, IBackgroundAction<TCommand>
    {
        // Register typed handler for resolution by orchestrator
        services.AddScoped<IBackgroundAction<TCommand>, TAction>();

        // Register concrete implementation for dependency graph
        services.AddScoped<TAction>();

        return services;
    }
}
