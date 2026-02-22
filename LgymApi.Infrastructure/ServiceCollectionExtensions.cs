using Hangfire;
using Hangfire.PostgreSql;
using LgymApi.Application.Notifications;
using LgymApi.Application.Services;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Jobs;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.Application.Repositories;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Mail;

namespace LgymApi.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool enableSensitiveLogging, bool isTesting = false)
    {
        var emailOptions = new EmailOptions
        {
            Enabled = bool.TryParse(configuration["Email:Enabled"], out var enabled) && enabled,
            FromAddress = configuration["Email:FromAddress"] ?? string.Empty,
            FromName = configuration["Email:FromName"] ?? "LGYM Trainer",
            SmtpHost = configuration["Email:SmtpHost"] ?? string.Empty,
            SmtpPort = int.TryParse(configuration["Email:SmtpPort"], out var smtpPort) ? smtpPort : 587,
            Username = configuration["Email:Username"] ?? string.Empty,
            Password = configuration["Email:Password"] ?? string.Empty,
            UseSsl = GetBooleanOrDefault(configuration["Email:UseSsl"], defaultValue: true),
            InvitationBaseUrl = configuration["Email:InvitationBaseUrl"] ?? string.Empty,
            TemplateRootPath = configuration["Email:TemplateRootPath"] ?? "EmailTemplates",
            DefaultCulture = ResolveDefaultCulture(configuration["Email:DefaultCulture"])
        };

        ValidateEmailOptions(emailOptions);
        services.AddSingleton(emailOptions);
        services.AddSingleton<IEmailNotificationsFeature, EmailNotificationsFeature>();

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
            services.AddHangfireServer();
            services.AddScoped<IInvitationEmailBackgroundScheduler, HangfireInvitationEmailBackgroundScheduler>();
        }
        else
        {
            services.AddScoped<IInvitationEmailBackgroundScheduler, NoOpInvitationEmailBackgroundScheduler>();
        }

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ILegacyPasswordService, LegacyPasswordService>();
        services.AddScoped<LgymApi.Application.Services.IRankService, LgymApi.Application.Services.RankService>();
        services.AddSingleton<IUserSessionCache, UserSessionCache>();
        services.AddScoped<IEmailTemplateComposer, TrainerInvitationEmailTemplateComposer>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<InvitationEmailJob>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ITrainerRelationshipRepository, TrainerRelationshipRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IEmailNotificationLogRepository, EmailNotificationLogRepository>();
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
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }

    private static void ValidateEmailOptions(EmailOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.InvitationBaseUrl))
        {
            throw new InvalidOperationException("Email:InvitationBaseUrl is required.");
        }

        if (!Uri.TryCreate(options.InvitationBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Email:InvitationBaseUrl must be a valid absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(options.TemplateRootPath))
        {
            throw new InvalidOperationException("Email:TemplateRootPath is required when email is enabled.");
        }

        if (options.DefaultCulture == null)
        {
            throw new InvalidOperationException("Email:DefaultCulture is required when email is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new InvalidOperationException("Email:FromAddress is required when email is enabled.");
        }

        try
        {
            _ = new MailAddress(options.FromAddress);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Email:FromAddress must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            throw new InvalidOperationException("Email:SmtpHost is required when email is enabled.");
        }

        if (options.SmtpPort <= 0)
        {
            throw new InvalidOperationException("Email:SmtpPort must be greater than 0 when email is enabled.");
        }
    }

    private static CultureInfo ResolveDefaultCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CultureInfo.GetCultureInfo("en-US");
        }

        try
        {
            return CultureInfo.GetCultureInfo(value);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }

    private static bool GetBooleanOrDefault(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
