using LgymApi.Application.Repositories;
using LgymApi.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrainingPlanningInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IPlanDayRepository, PlanDayRepository>();
        services.AddScoped<IPlanDayExerciseRepository, PlanDayExerciseRepository>();

        return services;
    }
}
