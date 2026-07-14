using LgymApi.Application.Features.DietPlans;
using LgymApi.Application.Features.Supplementation;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Nutrition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNutritionModule(this IServiceCollection services)
    {
        services.AddScoped<IDietPlanService, DietPlanService>();
        services.AddScoped<ISupplementationService, SupplementationService>();

        return services;
    }
}
