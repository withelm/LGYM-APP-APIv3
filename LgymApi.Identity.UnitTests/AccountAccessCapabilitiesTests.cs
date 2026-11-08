using FluentAssertions;
using LgymApi.Application.Identity.Authorization;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using NSubstitute;

namespace LgymApi.Identity.UnitTests;

[TestFixture]
public sealed class AccountAccessCapabilitiesTests
{
    private static AuthenticatedAccountContext Account() => new(
        Id<AccountReference>.New(), Id<AccountSessionReference>.New(), ["Admin", "Trainer"], [], false, false);

    [Test]
    public void MissingExtensionsAndDeniedPolicies_DoNotFallBackToRoles()
    {
        var service = new AuthenticatedAccountAccessCapabilitiesService(
            Substitute.For<IAuthenticatedAccountAuthorizationService>(), []);

        var result = service.GetCapabilities(Account());

        result.Application.Should().Be(new AccountAccessApplicationCapabilities(false, false, false, false, false));
        result.Extensions.Should().Be(new AccountAccessOwnerExtensions(
            new(false), new(false), new(false), new(false), new(false), new(false, false, false, false)));
    }

    [Test]
    public void ApplicationCapabilities_UseEachCorrespondingPolicyDecision()
    {
        var account = Account();
        var authorization = Substitute.For<IAuthenticatedAccountAuthorizationService>();
        authorization.HasPermission(account, AuthConstants.Permissions.TrainerAccess).Returns(true);
        authorization.HasPermission(account, AuthConstants.Permissions.ManageAppConfig).Returns(true);
        var service = new AuthenticatedAccountAccessCapabilitiesService(authorization, []);

        var result = service.GetCapabilities(account);

        result.AccountId.Should().Be(account.Id);
        result.Application.Should().Be(new AccountAccessApplicationCapabilities(false, false, true, false, true));
    }

    [Test]
    public void RegisteredExtension_AdvertisesAvailabilityWithoutGrantingActions()
    {
        var service = new AuthenticatedAccountAccessCapabilitiesService(
            Substitute.For<IAuthenticatedAccountAuthorizationService>(), [Extension(AccountAccessCapabilityOwner.Reporting)]);

        var result = service.GetCapabilities(Account());

        result.Extensions.Reporting.IsAvailable.Should().BeTrue();
        result.Extensions.Coaching.IsAvailable.Should().BeFalse();
        result.Application.Should().Be(new AccountAccessApplicationCapabilities(false, false, false, false, false));
    }

    [Test]
    public void DuplicateAndUnknownExtensions_StayUnavailable()
    {
        var service = new AuthenticatedAccountAccessCapabilitiesService(
            Substitute.For<IAuthenticatedAccountAuthorizationService>(),
            [Extension(AccountAccessCapabilityOwner.Reporting), Extension(AccountAccessCapabilityOwner.Reporting),
             Extension(AccountAccessCapabilityOwner.Unknown), Extension((AccountAccessCapabilityOwner)999)]);

        var result = service.GetCapabilities(Account());

        result.Extensions.Should().Be(new AccountAccessOwnerExtensions(
            new(false), new(false), new(false), new(false), new(false), new(false, false, false, false)));
    }

    private static IAccountAccessOwnerCapabilityExtension Extension(AccountAccessCapabilityOwner owner)
    {
        var extension = Substitute.For<IAccountAccessOwnerCapabilityExtension>();
        extension.Owner.Returns(owner);
        extension.GetAvailability().Returns(new AccountAccessOwnerCapabilities(true));
        return extension;
    }
}
