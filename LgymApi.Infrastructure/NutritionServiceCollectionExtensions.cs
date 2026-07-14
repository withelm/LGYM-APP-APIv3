using LgymApi.Application.Repositories;
using LgymApi.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddNutritionInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDietPlanRepository, DietPlanRepository>();
        services.AddScoped<ISupplementationRepository, SupplementationRepository>();

        return services;
    }
}
