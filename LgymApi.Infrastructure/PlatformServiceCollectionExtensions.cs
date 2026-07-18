using Hangfire;
using Hangfire.PostgreSql;
using LgymApi.Application.Options;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Configuration;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Pagination;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GridifyExecutionServiceContract = LgymApi.Infrastructure.Pagination.IGridifyExecutionService;
using QueryPaginationFacade = LgymApi.Infrastructure.Pagination.QueryPaginationService;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableSensitiveLogging,
        bool isTesting = false,
        bool hostBackgroundServer = false)
    {
        var appDefaultsOptions = AppDefaultsOptionsFactory.Resolve(configuration);
        var backgroundCommandOptions = configuration.GetSection("BackgroundCommands").Get<BackgroundCommandOptions>() ?? new BackgroundCommandOptions();
        var emailOptions = EmailOptionsFactory.Create(configuration, appDefaultsOptions);

        backgroundCommandOptions.Validate();
        EmailOptionsFactory.Validate(emailOptions);

        services.AddSingleton(appDefaultsOptions);
        services.AddSingleton(backgroundCommandOptions);
        services.AddSingleton(emailOptions);
        services.AddHttpContextAccessor();
        // Google auth fallback uses Google userinfo over HTTP when the ID token omits profile/email claims.
        services.AddHttpClient();
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
        services.AddScoped<IAppConfigRepository, AppConfigRepository>();
        services.AddScoped<ICommandEnvelopeRepository, CommandEnvelopeRepository>();
        services.AddScoped<IApiIdempotencyRecordRepository, ApiIdempotencyRecordRepository>();

        return services;
    }
}
