using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.Notifications;

internal sealed class NotificationsAccountAccessCapabilityExtension :
    IAccountAccessOwnerCapabilityExtension<NotificationsAccountAccessOwnerCapabilities>
{
    public AccountAccessCapabilityOwner Owner => AccountAccessCapabilityOwner.Notifications;

    public AccountAccessOwnerCapabilities GetAvailability() => new(true);

    public NotificationsAccountAccessOwnerCapabilities GetCapabilities(
        AuthenticatedAccountContext accountContext)
    {
        var canAccess = accountContext.SessionId.HasValue
            && !accountContext.IsBlocked
            && !accountContext.IsDeleted;
        return new NotificationsAccountAccessOwnerCapabilities(
            true,
            canAccess,
            canAccess,
            canAccess);
    }
}
