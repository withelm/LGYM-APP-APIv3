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

        Assert.That((services).Where(descriptor =>
            descriptor.ServiceType == typeof(IAuthorizationPolicy<NotificationOwnershipContext>)
            && descriptor.ImplementationType == typeof(NotificationOwnershipPolicy)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor =>
            descriptor.ServiceType == typeof(IAccountAccessOwnerCapabilityExtension)
            && descriptor.ImplementationType == typeof(NotificationsAccountAccessCapabilityExtension)), Has.Exactly(1).Items);
    }

    [Test]
    public void AddNotificationsModule_RejectsDuplicateOwnershipPolicyRegistration()
    {
        var services = new ServiceCollection();
        services.AddNotificationsModule();

        var act = () => services.AddNotificationsModule();

        Assert.That(() => { act(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*NotificationOwnershipContext*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void CapabilityExtension_AdvertisesNotificationsAndGrantsAnActiveSession()
    {
        var extension = new NotificationsAccountAccessCapabilityExtension();

        var capabilities = extension.GetCapabilities(CreateAccountContext());

        Assert.That(extension.Owner, Is.EqualTo(AccountAccessCapabilityOwner.Notifications));
        Assert.That(extension.GetAvailability(), Is.EqualTo(new AccountAccessOwnerCapabilities(true)));
        Assert.That(capabilities, Is.EqualTo(new NotificationsAccountAccessOwnerCapabilities(true, true, true, true)));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void CapabilityExtension_DeniesActionsForUnavailableAccounts(bool isBlocked, bool isDeleted)
    {
        var extension = new NotificationsAccountAccessCapabilityExtension();

        var capabilities = extension.GetCapabilities(CreateAccountContext(isBlocked: isBlocked, isDeleted: isDeleted));

        Assert.That(capabilities, Is.EqualTo(new NotificationsAccountAccessOwnerCapabilities(true, false, false, false)));
    }

    [Test]
    public void CapabilityExtension_DeniesActionsWithoutAnActiveSession()
    {
        var extension = new NotificationsAccountAccessCapabilityExtension();

        var capabilities = extension.GetCapabilities(CreateAccountContext(hasSession: false));

        Assert.That(capabilities, Is.EqualTo(new NotificationsAccountAccessOwnerCapabilities(true, false, false, false)));
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
