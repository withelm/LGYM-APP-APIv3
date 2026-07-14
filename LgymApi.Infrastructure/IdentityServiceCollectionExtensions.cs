using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<ILegacyPasswordService, LegacyPasswordService>();
        services.AddScoped<IUserSessionStore, UserSessionStore>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserExternalLoginRepository, UserExternalLoginRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IEloRegistryRepository, EloRegistryRepository>();
        services.AddScoped<ITutorialProgressRepository, TutorialProgressRepository>();

        return services;
    }
}
