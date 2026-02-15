using LgymApi.Application.Services;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using LgymApi.Application.Repositories;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool enableSensitiveLogging)
    {
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

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ILegacyPasswordService, LegacyPasswordService>();
        services.AddScoped<LgymApi.Application.Services.IRankService, LgymApi.Application.Services.RankService>();
        services.AddSingleton<IUserSessionCache, UserSessionCache>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
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

        return services;
    }
}
