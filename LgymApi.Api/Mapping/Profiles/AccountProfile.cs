using LgymApi.Api.Features.Account.Contracts;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Mapping.Core;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class AccountProfile : IMappingProfile
{
    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<ExternalLoginProjection, ExternalLoginDto>((source, _) => new ExternalLoginDto
        {
            Provider = source.Provider,
            ProviderEmail = source.ProviderEmail
        });
        configuration.CreateMap<AccountAccessCapabilities, AccountAccessResponseDto>((source, context) => new AccountAccessResponseDto
        {
            SchemaVersion = 1,
            Account = context!.Map<AccountAccessAccountDto>(source.AccountId),
            Application = context.Map<AccountAccessApplicationCapabilitiesDto>(source.Application),
            Extensions = context.Map<AccountAccessOwnerExtensionsDto>(source.Extensions)
        });
        configuration.CreateMap<LgymApi.Domain.ValueObjects.Id<AccountReference>, AccountAccessAccountDto>((source, _) => new AccountAccessAccountDto
        {
            Id = source.ToString()
        });
        configuration.CreateMap<AccountAccessApplicationCapabilities, AccountAccessApplicationCapabilitiesDto>((source, _) => new AccountAccessApplicationCapabilitiesDto
        {
            CanAccessAdminWorkspace = source.CanAccessAdminWorkspace,
            CanManageUserRoles = source.CanManageUserRoles,
            CanManageAppConfig = source.CanManageAppConfig,
            CanManageGlobalExercises = source.CanManageGlobalExercises,
            CanAccessTrainerWorkspace = source.CanAccessTrainerWorkspace
        });
        configuration.CreateMap<AccountAccessOwnerExtensions, AccountAccessOwnerExtensionsDto>((source, context) => new AccountAccessOwnerExtensionsDto
        {
            Reporting = context!.Map<AccountAccessOwnerCapabilitiesDto>(source.Reporting),
            TrainingPlanning = context.Map<AccountAccessOwnerCapabilitiesDto>(source.TrainingPlanning),
            Coaching = context.Map<AccountAccessOwnerCapabilitiesDto>(source.Coaching),
            Nutrition = context.Map<AccountAccessOwnerCapabilitiesDto>(source.Nutrition),
            WorkoutProgress = context.Map<AccountAccessOwnerCapabilitiesDto>(source.WorkoutProgress),
            Notifications = context.Map<NotificationsAccountAccessOwnerCapabilitiesDto>(source.Notifications)
        });
        configuration.CreateMap<AccountAccessOwnerCapabilities, AccountAccessOwnerCapabilitiesDto>((source, _) => new AccountAccessOwnerCapabilitiesDto
        {
            IsAvailable = source.IsAvailable
        });
        configuration.CreateMap<NotificationsAccountAccessOwnerCapabilities, NotificationsAccountAccessOwnerCapabilitiesDto>((source, _) => new NotificationsAccountAccessOwnerCapabilitiesDto
        {
            IsAvailable = source.IsAvailable,
            CanReadNotifications = source.CanReadNotifications,
            CanMarkNotificationsRead = source.CanMarkNotificationsRead,
            CanManagePushInstallations = source.CanManagePushInstallations
        });
    }
}
