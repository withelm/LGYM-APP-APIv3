using Hangfire;
using Hangfire.PostgreSql;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Options;
using LgymApi.Application.Pagination;
using LgymApi.Infrastructure.Pagination;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.Domain.Entities;
using LgymApi.Application.Options;
using LgymApi.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LgymApi.Infrastructure.Configuration;
using GridifyExecutionServiceContract = LgymApi.Infrastructure.Pagination.IGridifyExecutionService;
using QueryPaginationFacade = LgymApi.Infrastructure.Pagination.QueryPaginationService;

namespace LgymApi.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLogging,
        bool isTesting = false,
        bool hostBackgroundServer = false)
    {
        var appDefaultsOptions = AppDefaultsOptionsFactory.Resolve(configuration);

        var emailOptions = EmailOptionsFactory.Create(configuration, appDefaultsOptions);

        services.AddSingleton(appDefaultsOptions);
        EmailOptionsFactory.Validate(emailOptions);
        services.AddSingleton(emailOptions);
        services.AddSingleton<IEmailNotificationsFeature, EmailNotificationsFeature>();
        services.AddSingleton<IEmailMetrics, EmailMetrics>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            options
                .UseLoggerFactory(loggerFactory)
                .UseNpgsql(configuration.GetConnectionString("Postgres"));

            if (enableSensitiveLogging)
            {
                options
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }
        });

        if (!isTesting)
        {
            services.AddHangfire(hangfire =>
            {
                hangfire
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UsePostgreSqlStorage(storage =>
                    {
                        storage.UseNpgsqlConnection(configuration.GetConnectionString("Postgres"));
                    });
            });

            if (hostBackgroundServer)
            {
                services.AddHangfireServer();
            }
        }

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ILegacyPasswordService, LegacyPasswordService>();
        services.AddScoped<IUserSessionStore, UserSessionStore>();
        services.AddScoped<IEmailTemplateComposer, TrainerInvitationEmailTemplateComposer>();
        services.AddScoped<IEmailTemplateComposer, TrainerInvitationAcceptedEmailTemplateComposer>();
        services.AddScoped<IEmailTemplateComposer, TrainerInvitationRevokedEmailTemplateComposer>();
        services.AddScoped<IEmailTemplateComposer, TrainingCompletedEmailTemplateComposer>();
        services.AddScoped<IEmailTemplateComposer, WelcomeEmailTemplateComposer>();
        services.AddScoped<IEmailTemplateComposer, PasswordRecoveryEmailTemplateComposer>();
        services.AddScoped<IEmailTemplateComposerFactory, EmailTemplateComposerFactory>();
        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<DummyEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
        {
            var options = sp.GetRequiredService<EmailOptions>();
            return options.DeliveryMode == EmailDeliveryMode.Dummy
                ? sp.GetRequiredService<DummyEmailSender>()
                : sp.GetRequiredService<SmtpEmailSender>();
        });
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ITrainerRelationshipRepository, TrainerRelationshipRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<ISupplementationRepository, SupplementationRepository>();
        services.AddScoped<IEmailNotificationLogRepository, EmailNotificationLogRepository>();
        services.AddScoped<IEmailNotificationSubscriptionRepository, EmailNotificationSubscriptionRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IPlanDayRepository, PlanDayRepository>();
        services.AddScoped<IPlanDayExerciseRepository, PlanDayExerciseRepository>();
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        services.AddScoped<ITrainingRepository, TrainingRepository>();
        services.AddScoped<ITrainingExerciseScoreRepository, TrainingExerciseScoreRepository>();
        services.AddScoped<IExerciseScoreRepository, ExerciseScoreRepository>();
        services.AddScoped<IMeasurementRepository, MeasurementRepository>();
        services.AddScoped<IMainRecordRepository, MainRecordRepository>();
        services.AddScoped<IGymRepository, GymRepository>();
        services.AddScoped<IEloRegistryRepository, EloRegistryRepository>();
        services.AddScoped<IAppConfigRepository, AppConfigRepository>();
        services.AddScoped<ICommandEnvelopeRepository, CommandEnvelopeRepository>();
        services.AddScoped<ITutorialProgressRepository, TutorialProgressRepository>();
        services.AddScoped<IApiIdempotencyRecordRepository, ApiIdempotencyRecordRepository>();
        services.AddScoped<IInAppNotificationRepository, InAppNotificationRepository>();
        services.AddScoped<GridifyExecutionService>();
        services.AddScoped<GridifyExecutionServiceContract>(sp => sp.GetRequiredService<GridifyExecutionService>());
        services.AddScoped<IQueryPaginationService, QueryPaginationFacade>();
        services.AddSingleton<IMapperRegistry>(sp =>
        {
            var registry = new MapperRegistry();
            InfrastructureMappingRegistration.RegisterAll(registry);
            return registry;
        });
        services.AddSingleton(new PaginationPolicy
        {
            MaxPageSize = 100,
            DefaultPageSize = 20,
            DefaultSortField = "id",
            TieBreakerField = "id"
        });
        services.AddScoped<ICommittedIntentDispatcher, CommittedIntentDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}
