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
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LgymApi.Infrastructure.Configuration;
using LgymApi.BackgroundWorker.Common.Push;
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
        var isDevelopmentOrTesting = enableSensitiveLogging || isTesting;
        var appDefaultsOptions = AppDefaultsOptionsFactory.Resolve(configuration);
        var photoStorageOptions = BuildPhotoStorageOptions(configuration);
        var backgroundCommandOptions = configuration.GetSection("BackgroundCommands").Get<BackgroundCommandOptions>() ?? new BackgroundCommandOptions();
        backgroundCommandOptions.Validate();

        var emailOptions = EmailOptionsFactory.Create(configuration, appDefaultsOptions);
        var pushNotificationOptions = PushNotificationOptionsFactory.Create(configuration);

        services.AddSingleton(appDefaultsOptions);
        services.AddSingleton(photoStorageOptions);
        services.AddSingleton(backgroundCommandOptions);
        EmailOptionsFactory.Validate(emailOptions);
        services.AddSingleton(emailOptions);
        PushNotificationOptionsFactory.Validate(pushNotificationOptions);
        services.AddSingleton(pushNotificationOptions);
        services.AddHttpContextAccessor();
        // Google auth fallback uses Google userinfo over HTTP when the ID token omits profile/email claims.
        services.AddHttpClient();
        services.AddSingleton<IEmailNotificationsFeature, EmailNotificationsFeature>();
        services.AddSingleton<IEmailMetrics, EmailMetrics>();
        services.AddSingleton<IStalePushInstallationCleanupSettings, PushInstallationCleanupSettings>();

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
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
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
        services.AddSingleton<LocalPhotoDevelopmentStore>();
        services.AddSingleton<InMemoryPhotoUploadInitTracker>();
        services.AddScoped<IPushProviderSender, FcmPushSender>();
        services.AddScoped<IPushBackgroundScheduler, NoOpPushBackgroundScheduler>();
        if (!isTesting)
        {
            services.AddScoped<IPushBackgroundScheduler, HangfirePushBackgroundScheduler>();
        }

        services.AddScoped<IPhotoUploadInitTracker, DbPhotoUploadInitTracker>();
        RegisterPhotoStorageProvider(services, photoStorageOptions, isDevelopmentOrTesting);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPushInstallationRepository, PushInstallationRepository>();
        services.AddScoped<IPushNotificationMessageRepository, PushNotificationMessageRepository>();
        services.AddScoped<IUserExternalLoginRepository, UserExternalLoginRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ITrainerRelationshipRepository, TrainerRelationshipRepository>();
        services.AddScoped<IDietPlanRepository, DietPlanRepository>();
        services.AddScoped<ITraineeNoteRepository, TraineeNoteRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IRecurringReportAssignmentRepository, RecurringReportAssignmentRepository>();
        services.AddScoped<ISupplementationRepository, SupplementationRepository>();
        services.AddScoped<IEmailNotificationLogRepository>(sp =>
            new EmailNotificationLogRepository(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<BackgroundCommandOptions>()));
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

    private static void RegisterPhotoStorageProvider(
        IServiceCollection services,
        PhotoStorageOptions options,
        bool isDevelopmentOrTesting)
    {
        if (string.Equals(options.Provider, "CloudflareR2", StringComparison.OrdinalIgnoreCase))
        {
            ValidateCloudflareR2Options(options);
            services.AddScoped<IPhotoStorageProvider, CloudflareR2PhotoStorageProvider>();
            return;
        }

        if (string.Equals(options.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            if (!isDevelopmentOrTesting)
            {
                throw new InvalidOperationException("LocalPhotoStorageProvider cannot be used outside Development.");
            }

            services.AddScoped<IPhotoStorageProvider, LocalPhotoStorageProvider>();
            return;
        }

        throw new InvalidOperationException($"Unsupported photo storage provider: {options.Provider}");
    }

    private static PhotoStorageOptions BuildPhotoStorageOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection("PhotoStorage").Get<PhotoStorageOptions>() ?? new PhotoStorageOptions();

        options.Provider = string.IsNullOrWhiteSpace(options.Provider) ? "Local" : options.Provider.Trim();
        options.AllowedMimeTypes = options.AllowedMimeTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (options.AllowedMimeTypes.Count == 0)
        {
            options.AllowedMimeTypes = ["image/jpeg", "image/png", "image/heic"];
        }

        return options;
    }

    private static void ValidateCloudflareR2Options(PhotoStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            throw new InvalidOperationException("PhotoStorage:BucketName is required for CloudflareR2.");
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("PhotoStorage:Endpoint is required for CloudflareR2.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
        {
            throw new InvalidOperationException("PhotoStorage:AccessKeyId is required for CloudflareR2.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            throw new InvalidOperationException("PhotoStorage:SecretAccessKey is required for CloudflareR2.");
        }
    }
}
