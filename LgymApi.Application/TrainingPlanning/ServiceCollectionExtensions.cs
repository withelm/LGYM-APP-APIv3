using LgymApi.Application.Features.Plan;
using LgymApi.Application.Features.PlanDay;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.TrainingPlanning;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrainingPlanningModule(this IServiceCollection services)
    {
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IPlanDayServiceDependencies, PlanDayServiceDependencies>();
        services.AddScoped<IPlanDayService, PlanDayService>();

        return services;
    }
}
