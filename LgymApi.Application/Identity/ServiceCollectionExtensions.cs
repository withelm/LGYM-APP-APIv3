using LgymApi.Application.ExternalAuth;
using LgymApi.Application.Features.AdminManagement;
using LgymApi.Application.Features.PasswordReset;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Application.Identity;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<PasswordResetServiceDependencies>();
        services.AddScoped<IPasswordResetTokenGenerationService, PasswordResetTokenGenerationService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IRankService, RankService>();
        services.AddScoped<IAccountLinkingService, AccountLinkingService>();
        services.AddScoped<IExternalAuthService, ExternalAuthService>();
        services.AddScoped<IGoogleUserRegistrar, GoogleUserRegistrar>();
        services.AddScoped<ILoginResultBuilder, LoginResultBuilder>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserServiceDependencies, UserServiceDependencies>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITutorialService, TutorialService>();

        return services;
    }
}
