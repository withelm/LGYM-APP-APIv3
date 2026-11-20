using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Adapters;
using LgymApi.Identity.Contracts;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PushInstallationAccountCleanupAdapterTests
{
    [Test]
    public async Task StageRemoveForAccountAsync_ForwardsMarkerAccountExactlyOnceWithoutCommitting()
    {
        var lifecycleService = Substitute.For<IPushInstallationLifecycleService>();
        var adapter = new PushInstallationAccountCleanupAdapter(lifecycleService);
        var accountId = Id<AccountReference>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        await adapter.StageRemoveForAccountAsync(accountId, cancellationToken);

        await lifecycleService.Received(1).StageRemoveForAccountAsync(accountId, cancellationToken);
    }

    [Test]
    public void AddNotificationsModule_RegistersAccountPushInstallationCleanupPortExactlyOnceAsScoped()
    {
        var services = new ServiceCollection();

        services.AddNotificationsModule();

        var assertedValue143 = (services
                    .Where(descriptor => descriptor.ServiceType == typeof(IAccountPushInstallationCleanupPort))).ToArray();
        Assert.That(assertedValue143, Has.Exactly(1).Items);
        var registration = assertedValue143.Single();
        Assert.That(registration.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(registration.ImplementationType, Is.EqualTo(typeof(PushInstallationAccountCleanupAdapter)));
    }
}
