using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.Notifications.UnitTests;

[TestFixture]
public sealed class NotificationsModuleTests
{
    [Test]
    public void AddNotificationsModule_RegistersTheOwnershipPolicyAndCapabilityExtension()
    {
        var services = new ServiceCollection();

        services.AddNotificationsModule();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IAuthorizationPolicy<NotificationOwnershipContext>)
            && descriptor.ImplementationType == typeof(NotificationOwnershipPolicy));
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IAccountAccessOwnerCapabilityExtension)
            && descriptor.ImplementationType == typeof(NotificationsAccountAccessCapabilityExtension));
    }

    [Test]
    public void AddNotificationsModule_RejectsDuplicateOwnershipPolicyRegistration()
    {
        var services = new ServiceCollection();
        services.AddNotificationsModule();

        var act = () => services.AddNotificationsModule();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NotificationOwnershipContext*");
    }

    [Test]
    public void CapabilityExtension_AdvertisesNotificationsAndGrantsAnActiveSession()
    {
        var extension = new NotificationsAccountAccessCapabilityExtension();

        var capabilities = extension.GetCapabilities(CreateAccountContext());

        extension.Owner.Should().Be(AccountAccessCapabilityOwner.Notifications);
        extension.GetAvailability().Should().Be(new AccountAccessOwnerCapabilities(true));
        capabilities.Should().Be(new NotificationsAccountAccessOwnerCapabilities(true, true, true, true));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void CapabilityExtension_DeniesActionsForUnavailableAccounts(bool isBlocked, bool isDeleted)
    {
        var extension = new NotificationsAccountAccessCapabilityExtension();

        var capabilities = extension.GetCapabilities(CreateAccountContext(isBlocked: isBlocked, isDeleted: isDeleted));

        capabilities.Should().Be(new NotificationsAccountAccessOwnerCapabilities(true, false, false, false));
    }

    [Test]
    public void CapabilityExtension_DeniesActionsWithoutAnActiveSession()
    {
        var extension = new NotificationsAccountAccessCapabilityExtension();

        var capabilities = extension.GetCapabilities(CreateAccountContext(hasSession: false));

        capabilities.Should().Be(new NotificationsAccountAccessOwnerCapabilities(true, false, false, false));
    }

    private static AuthenticatedAccountContext CreateAccountContext(
        bool hasSession = true,
        bool isBlocked = false,
        bool isDeleted = false) =>
        new(
            Id<AccountReference>.New(),
            hasSession ? Id<AccountSessionReference>.New() : null,
            [],
            [],
            isBlocked,
            isDeleted);
}
