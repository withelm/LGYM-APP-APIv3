using LgymApi.Domain.Security;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.Application.Nutrition;

internal sealed class NutritionAccountAccessCapabilitiesExtension : IAccountAccessOwnerCapabilityExtension<NutritionAccountAccessOwnerCapabilities>
{
    private readonly IAuthenticatedAccountAuthorizationService _authorization;

    public NutritionAccountAccessCapabilitiesExtension(IAuthenticatedAccountAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    public AccountAccessCapabilityOwner Owner => AccountAccessCapabilityOwner.Nutrition;

    public AccountAccessOwnerCapabilities GetAvailability() => new(true);

    public NutritionAccountAccessOwnerCapabilities GetCapabilities(AuthenticatedAccountContext accountContext)
    {
        var canUseTrainerActions = _authorization.HasPermission(accountContext, AuthConstants.Permissions.TrainerAccess);
        return new NutritionAccountAccessOwnerCapabilities(
            true,
            true,
            true,
            canUseTrainerActions,
            canUseTrainerActions);
    }
}
