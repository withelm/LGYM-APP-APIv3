using LgymApi.Application;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Nutrition.DietPlans.CreateTraineePlan;
using LgymApi.Application.Notifications;
using LgymApi.Application.Reporting;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Platform.ReferenceData.AppConfig;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Application.Platform.ReferenceData.ApiAdapters;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.TrainingPlanning.PlanDay.Persistence;
using LgymApi.Application.WorkoutProgress.ProgressData;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Services;
using LgymApi.Notifications.ApiAdapters;
using LgymApi.Notifications.Contracts;
using LgymApi.Platform.Contracts;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CompositionFacadeOwnershipTests
{
    [Test]
    public void OrderedHostComposition_RegistersAndResolvesExactOwnedDescriptorsOnce()
    {
        var services = CompositionRootTestHost.Create();
        var expectations = CreateExpectations();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        foreach (var expectation in expectations)
        {
            var descriptors = services
                .Where(candidate => candidate.ServiceType == expectation.ServiceType)
                .ToArray();
            Assert.That(descriptors, Has.Exactly(1).Items, expectation.ServiceType.FullName);
            var descriptor = descriptors.Single();
            Assert.That(descriptor.Lifetime, Is.EqualTo(expectation.Lifetime), expectation.ServiceType.FullName);
            Assert.That(descriptor.ImplementationType, Is.Not.Null, expectation.ServiceType.FullName);
            Assert.That(descriptor.ImplementationType!.Assembly, Is.SameAs(expectation.OwnerAssembly), expectation.ServiceType.FullName);

            var resolved = scope.ServiceProvider.GetRequiredService(expectation.ServiceType);
            Assert.That(resolved.GetType().Assembly, Is.SameAs(expectation.OwnerAssembly), expectation.ServiceType.FullName);
        }
    }

    [Test]
    public void AddApplication_RegistersOnlyWorkoutCoachingAndNutritionServices()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IReportingService)), Is.False);
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IWorkoutProgressReadWriteService)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(ICoachingRelationshipAccessService)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(ICreateTraineeDietPlanUseCase)), Has.Exactly(1).Items);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IAppConfigService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(ITokenService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IPlanDayService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IInAppNotificationService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IAppConfigApiAdapter)), Is.False);
    }

    [Test]
    public void AddReportingModule_RegistersReportingServices()
    {
        var services = new ServiceCollection();

        services.AddReportingModule();

        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IReportingService)), Has.Exactly(1).Items);
    }

    [Test]
    public void AddInfrastructure_RegistersRemainingPersistenceWithoutExtractedModuleImplementations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(
            CompositionRootTestHost.CreateConfiguration(),
            enableSensitiveLogging: false,
            isTesting: true,
            hostBackgroundServer: true);

        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IUnitOfWork)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IPlanDayPersistence)), Has.Exactly(1).Items);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IAppConfigService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(ITokenService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IPlanDayService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IInAppNotificationService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IEmailSender)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IPushProviderSender)), Is.False);
    }

    [Test]
    public void WorkerFacade_RegistersSchedulersAndRuntimeWithoutModuleProvidersOrRepositories()
    {
        var services = new ServiceCollection();

        services.AddBackgroundWorkerServices(isTesting: true);

        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IBackgroundActionResolver)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IEmailBackgroundScheduler)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IActionMessageScheduler)), Has.Exactly(1).Items);
        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IPushBackgroundScheduler)), Has.Exactly(1).Items);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IUnitOfWork)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IInAppNotificationService)), Is.False);
        Assert.That((services).Any(descriptor => descriptor.ServiceType == typeof(IPushProviderSender)), Is.False);
    }

    [Test]
    public void OrderedHostComposition_RejectsDuplicateCanonicalRepository()
    {
        var services = CompositionRootTestHost.Create();
        var repositoryDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IAppConfigRepository));
        services.AddScoped(repositoryDescriptor.ServiceType, repositoryDescriptor.ImplementationType!);

        var action = () => GetSingleDescriptor(services, typeof(IAppConfigRepository));

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IAppConfigRepository).FullName}*exactly once*2*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void HostComposition_RequiresWorkerAfterApiAdapters()
    {
        var services = CompositionRootTestHost.Create(registerWorkerBeforeInfrastructure: true);

        var action = () => AssertWorkerIsLast(services);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Worker*last*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private static FacadeDescriptorExpectation[] CreateExpectations() =>
    [
        new(typeof(IAppConfigService), ServiceLifetime.Scoped, typeof(ActorReference).Assembly),
        new(typeof(ITokenService), ServiceLifetime.Scoped, typeof(AccountReference).Assembly),
        new(typeof(IPlanDayService), ServiceLifetime.Scoped, typeof(PlanReference).Assembly),
        new(typeof(IInAppNotificationService), ServiceLifetime.Scoped, typeof(NotificationReference).Assembly),
        new(typeof(IReportingService), ServiceLifetime.Scoped, typeof(LgymApi.Application.Reporting.ServiceCollectionExtensions).Assembly),
        new(typeof(IWorkoutProgressReadWriteService), ServiceLifetime.Scoped, typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly),
        new(typeof(ICoachingRelationshipAccessService), ServiceLifetime.Scoped, typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly),
        new(typeof(ICreateTraineeDietPlanUseCase), ServiceLifetime.Scoped, typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly),
        new(typeof(IUnitOfWork), ServiceLifetime.Scoped, typeof(LgymApi.Infrastructure.ServiceCollectionExtensions).Assembly),
        new(typeof(IAppConfigApiAdapter), ServiceLifetime.Scoped, typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly),
        new(typeof(IPushInstallationApiAdapter), ServiceLifetime.Scoped, typeof(NotificationReference).Assembly),
        new(typeof(IBackgroundActionResolver), ServiceLifetime.Scoped, typeof(LgymApi.BackgroundWorker.ServiceProvider).Assembly),
        new(typeof(IEmailBackgroundScheduler), ServiceLifetime.Scoped, typeof(NoOpEmailBackgroundScheduler).Assembly)
    ];

    private static ServiceDescriptor GetSingleDescriptor(IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == serviceType).ToArray();
        if (descriptors.Length != 1)
        {
            throw new InvalidOperationException(
                $"Service '{serviceType.FullName}' must be registered exactly once; actual count is {descriptors.Length}.");
        }

        return descriptors[0];
    }

    private static void AssertWorkerIsLast(IServiceCollection services)
    {
        var workerIndex = services.IndexOf(GetSingleDescriptor(services, typeof(IEmailBackgroundScheduler)));
        var apiAdapterIndex = services.IndexOf(GetSingleDescriptor(services, typeof(IPushInstallationApiAdapter)));
        if (workerIndex <= apiAdapterIndex)
        {
            throw new InvalidOperationException("Worker registrations must be last after API adapters.");
        }
    }

    private sealed record FacadeDescriptorExpectation(
        Type ServiceType,
        ServiceLifetime Lifetime,
        System.Reflection.Assembly OwnerAssembly);
}
