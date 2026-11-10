using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.Reporting.Authorization;

internal sealed class ReportingAccountAccessCapabilitiesExtension
    : IAccountAccessOwnerCapabilityExtension<ReportingAccountAccessOwnerCapabilities>
{
    private readonly IReportingAuthorizationCoordinator _authorization;

    public ReportingAccountAccessCapabilitiesExtension(IReportingAuthorizationCoordinator authorization)
    {
        _authorization = authorization;
    }

    public AccountAccessCapabilityOwner Owner => AccountAccessCapabilityOwner.Reporting;

    public AccountAccessOwnerCapabilities GetAvailability() => new(true);

    public ReportingAccountAccessOwnerCapabilities GetCapabilities(AuthenticatedAccountContext accountContext)
    {
        var canManageOwnedDefinitions = _authorization.CanManageOwnedDefinition(accountContext, accountContext.Id);
        return new ReportingAccountAccessOwnerCapabilities(true, true, true, canManageOwnedDefinitions, canManageOwnedDefinitions);
    }
}
