using LgymApi.Application.AppConfig;
using LgymApi.Application.Features.Enum;
using LgymApi.Application.Units;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Platform;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services)
    {
        services.AddScoped<IAppConfigService, AppConfigService>();
        services.AddScoped<IEnumService, EnumService>();
        services.AddSingleton<ILinearUnitStrategy<WeightUnits>, WeightLinearUnitStrategy>();
        services.AddSingleton<IUnitConverter<WeightUnits>, LinearUnitConverter<WeightUnits>>();
        services.AddSingleton<ILinearUnitStrategy<HeightUnits>, HeightLinearUnitStrategy>();
        services.AddSingleton<IUnitConverter<HeightUnits>, LinearUnitConverter<HeightUnits>>();

        return services;
    }
}
