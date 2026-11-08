using LgymApi.Authorization.Contracts;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Identity.Contracts.Authorization;

public sealed record GlobalPermissionAuthorizationContext(
    string Permission,
    IReadOnlyList<string> PermissionClaims);

public interface IAuthenticatedAccountAuthorizationService
{
    AuthorizationActor CreateActor(AuthenticatedAccountContext accountContext);

    bool HasPermission(
        AuthenticatedAccountContext accountContext,
        string permission);
}
