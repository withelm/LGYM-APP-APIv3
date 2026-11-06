using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Identity.Contracts.Authorization;

public sealed record GlobalPermissionAuthorizationContext(
    string Permission,
    IReadOnlyList<string> PermissionClaims);

public interface IAuthenticatedAccountAuthorizationService
{
    bool HasPermission(
        AuthenticatedAccountContext accountContext,
        string permission);
}
