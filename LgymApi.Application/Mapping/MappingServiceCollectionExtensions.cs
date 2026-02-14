using System.Reflection;
using LgymApi.Application.Mapping.Core;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Mapping;

public static class MappingServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationMapping(this IServiceCollection services, params Assembly[]? assemblies)
    {
        var assembliesToScan = (assemblies != null && assemblies.Length > 0)
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        var profiles = assembliesToScan
            .SelectMany(GetLoadableTypes)
            .Where(type => typeof(IMappingProfile).IsAssignableFrom(type))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Distinct()
            .ToList();

        foreach (var profileType in profiles)
        {
            services.AddSingleton(typeof(IMappingProfile), profileType);
        }

        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<IEnumerable<IMappingProfile>>()));

        return services;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}
