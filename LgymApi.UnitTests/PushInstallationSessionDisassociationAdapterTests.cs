using LgymApi.Application.Identity.Contracts.Sessions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Adapters;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using LgymApi.UnitTests.Fakes;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PushInstallationSessionDisassociationAdapterTests
{
    [Test]
    public async Task StageDisassociateAsync_ForwardsTheSessionExactlyOnceWithoutCommitting()
    {
        var lifecycleService = Substitute.For<IPushInstallationLifecycleService>();
        var adapter = new PushInstallationSessionDisassociationAdapter(lifecycleService);
        var accountId = Id<AccountReference>.New();
        var sessionId = Id<AccountSessionReference>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        await adapter.StageDisassociateAsync(accountId, sessionId, cancellationToken);

        await lifecycleService.Received(1).StageDisassociateForSessionAsync(
            sessionId,
            cancellationToken);
    }

    [Test]
    public async Task AddNotificationsModule_RegistersAccountSessionDisassociationPortExactlyOnceAsScoped()
    {
        var services = new ServiceCollection();
        var pushInstallationRepository = new ConfigurablePushInstallationRepository();

        services.AddNotificationsModule();

        var assertedValue144 = (services
                    .Where(descriptor => descriptor.ServiceType == typeof(IPushInstallationRepository))).ToArray();
        Assert.That(assertedValue144, Has.Exactly(1).Items);
        var productionRepositoryRegistration = assertedValue144.Single();
        Assert.That(productionRepositoryRegistration.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(productionRepositoryRegistration.ImplementationType!.Name, Is.EqualTo("PushInstallationRepository"));

        services.AddScoped<IPushInstallationRepository>(_ => pushInstallationRepository);
        services.AddScoped<IPushInstallationPersistenceConflictClassifier>(_ => new NoRegistrationConflictClassifier());
        services.AddScoped(_ => Substitute.For<IUnitOfWork>());

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IAccountSessionDisassociationPort))
            .ToArray();

        Assert.That(registrations, Has.Exactly(1).Items);
        Assert.That(registrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(registrations[0].ImplementationType, Is.EqualTo(typeof(PushInstallationSessionDisassociationAdapter)));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var adapter = scope.ServiceProvider.GetRequiredService<IAccountSessionDisassociationPort>();
        Assert.That(adapter, Is.TypeOf<PushInstallationSessionDisassociationAdapter>());

        var accountId = Id<AccountReference>.New();
        var sessionId = Id<AccountSessionReference>.New();
        using var cancellationSource = new CancellationTokenSource();

        await adapter.StageDisassociateAsync(accountId, sessionId, cancellationSource.Token);

        Assert.That(pushInstallationRepository.Calls
            .Where(call =>
                call.Method == nameof(IPushInstallationRepository.DisassociateForSessionAsync)
                && call.Argument is ValueTuple<Id<AccountSessionReference>, DateTimeOffset> arguments
                && arguments.Item1 == sessionId
                && call.CancellationToken == cancellationSource.Token), Has.Exactly(1).Items);
    }

    private sealed class NoRegistrationConflictClassifier : IPushInstallationPersistenceConflictClassifier
    {
        public bool IsRegistrationConflict(DbUpdateException exception) => false;
    }
}
