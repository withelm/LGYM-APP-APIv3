using LgymApi.Authorization.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthorizationPolicy<TContext, TPolicy>(this IServiceCollection services)
        where TContext : notnull
        where TPolicy : class, IAuthorizationPolicy<TContext>
    {
        ArgumentNullException.ThrowIfNull(services);

        var policyServiceType = typeof(IAuthorizationPolicy<TContext>);
        if (services.Any(descriptor => descriptor.ServiceType == policyServiceType))
        {
            throw new InvalidOperationException(
                $"An authorization policy is already registered for '{typeof(TContext).FullName}'.");
        }

        services.AddScoped<IAuthorizationPolicy<TContext>, TPolicy>();
        services.AddScoped<IAuthorizationEvaluator<TContext>, AuthorizationPolicyEvaluator<TContext>>();

        return services;
    }
}
