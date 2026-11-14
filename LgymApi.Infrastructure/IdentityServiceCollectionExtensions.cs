using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Infrastructure.Repositories.AccountDeletion;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    private static void AddIdentityAccountDeletionInfrastructure(IServiceCollection services)
    {
        services.AddScoped<IAccountDeletionSynchronization, AccountDeletionSynchronization>();
        services.AddScoped<IAccountFinalizationStore, AccountFinalizationStore>();
        services.AddScoped<IAccountFinalizationCleanup, AccountFinalizationCleanup>();
        services.AddScoped<IAccountFinalizationPhotoManifest, AccountFinalizationPhotoManifest>();
    }
}
