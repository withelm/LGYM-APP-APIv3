using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Identity.Contracts.Authorization;

public enum AccountAccessCapabilityOwner
{
    Unknown = 0,
    Reporting = 1,
    TrainingPlanning = 2,
    Coaching = 3,
    Nutrition = 4,
    WorkoutProgress = 5,
    Notifications = 6
}

public sealed record AccountAccessOwnerCapabilities(bool IsAvailable);

public sealed record NutritionAccountAccessOwnerCapabilities(
    bool IsAvailable,
    bool CanReadOwnDietPlans,
    bool CanUpdateOwnDietPlans,
    bool CanCreateDietPlans,
    bool CanManageSupplementation);

public sealed record NotificationsAccountAccessOwnerCapabilities(
    bool IsAvailable,
    bool CanReadNotifications,
    bool CanMarkNotificationsRead,
    bool CanManagePushInstallations);

public interface IAccountAccessOwnerCapabilityExtension
{
    AccountAccessCapabilityOwner Owner { get; }

    AccountAccessOwnerCapabilities GetAvailability();
}

public interface IAccountAccessOwnerCapabilityExtension<out TCapabilities> : IAccountAccessOwnerCapabilityExtension
{
    TCapabilities GetCapabilities(AuthenticatedAccountContext accountContext);
}

public sealed record AccountAccessApplicationCapabilities(
    bool CanAccessAdminWorkspace,
    bool CanManageUserRoles,
    bool CanManageAppConfig,
    bool CanManageGlobalExercises,
    bool CanAccessTrainerWorkspace);

public sealed record AccountAccessOwnerExtensions(
    AccountAccessOwnerCapabilities Reporting,
    AccountAccessOwnerCapabilities TrainingPlanning,
    AccountAccessOwnerCapabilities Coaching,
    NutritionAccountAccessOwnerCapabilities Nutrition,
    AccountAccessOwnerCapabilities WorkoutProgress,
    NotificationsAccountAccessOwnerCapabilities Notifications);

public sealed record AccountAccessCapabilities(
    Id<AccountReference> AccountId,
    AccountAccessApplicationCapabilities Application,
    AccountAccessOwnerExtensions Extensions);

public interface IAuthenticatedAccountAccessCapabilitiesService
{
    AccountAccessCapabilities GetCapabilities(AuthenticatedAccountContext accountContext);
}
