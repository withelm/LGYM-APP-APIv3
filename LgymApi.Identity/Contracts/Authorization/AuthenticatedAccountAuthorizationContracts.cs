using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Enums;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Identity.Contracts.Authorization;

public sealed record GlobalPermissionAuthorizationContext(
    string Permission,
    IReadOnlyList<string> PermissionClaims,
    AccountDeletionState? DeletionState = null,
    bool IsBlocked = false,
    bool IsDeleted = false);

public interface IAuthenticatedAccountAuthorizationService
{
    AuthorizationActor CreateActor(AuthenticatedAccountContext accountContext);

    bool HasPermission(
        AuthenticatedAccountContext accountContext,
        string permission);
}
