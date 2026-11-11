using FluentAssertions;
using LgymApi.Application.Reporting.Authorization;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingAccountAccessCapabilitiesExtensionTests
{
    [TestCase(false, false)]
    [TestCase(true, true)]
    public void GetCapabilities_DefinitionCapabilitiesMatchTypedOwnershipDecision(bool trainerRole, bool expected)
    {
        var account = new AuthenticatedAccountContext(
            Id<AccountReference>.New(),
            null,
            trainerRole ? ["Trainer", "Admin"] : ["Admin"],
            [],
            false,
            false);
        var coordinator = ReportingAuthorizationTestFactory.Create();
        var extension = new ReportingAccountAccessCapabilitiesExtension(coordinator);

        var capabilities = extension.GetCapabilities(account);

        var policyDecision = coordinator.CanManageOwnedDefinition(account, account.Id);
        policyDecision.Should().Be(expected);
        capabilities.Should().Be(new ReportingAccountAccessOwnerCapabilities(true, true, true, policyDecision, policyDecision));
    }
}
