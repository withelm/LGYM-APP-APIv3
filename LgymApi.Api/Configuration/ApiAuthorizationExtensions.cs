using LgymApi.Api.Authorization;
using LgymApi.Api.Idempotency;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Api.Configuration;

public static class ApiAuthorizationExtensions
{
    public static IServiceCollection AddApiAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, CurrentPermissionAuthorizationHandler>();
        services.AddScoped<IApiIdempotencyReplayAccessPort, IdentityIdempotencyReplayAccessPort>();
        services.AddScoped<IApiIdempotencyReplayAccessPort, NotificationsIdempotencyReplayAccessPort>();
        services.AddScoped<ApiIdempotencyReplayAccessEvaluator>();

        services
            .AddAuthorizationBuilder()
            .AddPolicy(AuthConstants.Policies.AdminAccess, policy =>
                policy.AddRequirements(new CurrentPermissionRequirement(AuthConstants.Permissions.AdminAccess)))
            .AddPolicy(AuthConstants.Policies.ManageUserRoles, policy =>
                policy.AddRequirements(new CurrentPermissionRequirement(AuthConstants.Permissions.ManageUserRoles)))
            .AddPolicy(AuthConstants.Policies.ManageAppConfig, policy =>
                policy.AddRequirements(new CurrentPermissionRequirement(AuthConstants.Permissions.ManageAppConfig)))
            .AddPolicy(AuthConstants.Policies.ManageGlobalExercises, policy =>
                policy.AddRequirements(new CurrentPermissionRequirement(AuthConstants.Permissions.ManageGlobalExercises)))
            .AddPolicy(AuthConstants.Policies.TrainerAccess, policy =>
                policy.AddRequirements(new CurrentPermissionRequirement(AuthConstants.Permissions.TrainerAccess)));

        return services;
    }
}
