using LgymApi.Domain.Security;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.Application.Identity.Authorization;

internal sealed class AuthenticatedAccountAccessCapabilitiesService : IAuthenticatedAccountAccessCapabilitiesService
{
    private static readonly AccountAccessOwnerCapabilities Unavailable = new(false);
    private static readonly NotificationsAccountAccessOwnerCapabilities NotificationsUnavailable = new(false, false, false, false);
    private static readonly NutritionAccountAccessOwnerCapabilities NutritionUnavailable = new(false, false, false, false, false);
    private readonly IAuthenticatedAccountAuthorizationService _authorizationService;
    private readonly IReadOnlyDictionary<AccountAccessCapabilityOwner, IAccountAccessOwnerCapabilityExtension> _extensions;

    public AuthenticatedAccountAccessCapabilitiesService(
        IAuthenticatedAccountAuthorizationService authorizationService,
        IEnumerable<IAccountAccessOwnerCapabilityExtension> extensions)
    {
        _authorizationService = authorizationService;
        _extensions = extensions
            .Where(extension => extension.Owner != AccountAccessCapabilityOwner.Unknown && Enum.IsDefined(extension.Owner))
            .GroupBy(extension => extension.Owner)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
    }

    public AccountAccessCapabilities GetCapabilities(AuthenticatedAccountContext accountContext)
    {
        ArgumentNullException.ThrowIfNull(accountContext);

        return new AccountAccessCapabilities(
            accountContext.Id,
            new AccountAccessApplicationCapabilities(
                _authorizationService.HasPermission(accountContext, AuthConstants.Permissions.AdminAccess),
                _authorizationService.HasPermission(accountContext, AuthConstants.Permissions.ManageUserRoles),
                _authorizationService.HasPermission(accountContext, AuthConstants.Permissions.ManageAppConfig),
                _authorizationService.HasPermission(accountContext, AuthConstants.Permissions.ManageGlobalExercises),
                _authorizationService.HasPermission(accountContext, AuthConstants.Permissions.TrainerAccess)),
            new AccountAccessOwnerExtensions(
                GetOwnerCapabilities(AccountAccessCapabilityOwner.Reporting),
                GetOwnerCapabilities(AccountAccessCapabilityOwner.TrainingPlanning),
                GetOwnerCapabilities(AccountAccessCapabilityOwner.Coaching),
                GetNutritionCapabilities(accountContext),
                GetOwnerCapabilities(AccountAccessCapabilityOwner.WorkoutProgress),
                GetNotificationsCapabilities(accountContext)));
    }

    private AccountAccessOwnerCapabilities GetOwnerCapabilities(
        AccountAccessCapabilityOwner owner) =>
        _extensions.TryGetValue(owner, out var extension)
            ? extension.GetAvailability()
            : Unavailable;

    private NotificationsAccountAccessOwnerCapabilities GetNotificationsCapabilities(
        AuthenticatedAccountContext accountContext) =>
        _extensions.TryGetValue(AccountAccessCapabilityOwner.Notifications, out var extension)
            && extension is IAccountAccessOwnerCapabilityExtension<NotificationsAccountAccessOwnerCapabilities> typedExtension
                ? typedExtension.GetCapabilities(accountContext)
                : NotificationsUnavailable;

    private NutritionAccountAccessOwnerCapabilities GetNutritionCapabilities(
        AuthenticatedAccountContext accountContext) =>
        _extensions.TryGetValue(AccountAccessCapabilityOwner.Nutrition, out var extension)
        && extension is IAccountAccessOwnerCapabilityExtension<NutritionAccountAccessOwnerCapabilities> typedExtension
            ? typedExtension.GetCapabilities(accountContext)
            : NutritionUnavailable;
}
