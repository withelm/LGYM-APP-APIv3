using LgymApi.Application.Mapping.Core;
using LgymApi.Authorization.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.Application.Identity.Authorization;

internal sealed class AuthenticatedAccountAuthorizationService : IAuthenticatedAccountAuthorizationService
{
    private readonly IMapper _mapper;
    private readonly IAuthorizationEvaluator<GlobalPermissionAuthorizationContext> _evaluator;

    public AuthenticatedAccountAuthorizationService(
        IMapper mapper,
        IAuthorizationEvaluator<GlobalPermissionAuthorizationContext> evaluator)
    {
        _mapper = mapper;
        _evaluator = evaluator;
    }

    public bool HasPermission(AuthenticatedAccountContext accountContext, string permission)
    {
        ArgumentNullException.ThrowIfNull(accountContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var actor = _mapper.Map<AuthenticatedAccountContext, AuthorizationActor>(accountContext);
        return _evaluator.Evaluate(
            actor,
            new GlobalPermissionAuthorizationContext(permission, accountContext.PermissionClaims)).IsAllowed;
    }
}
