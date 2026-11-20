using System.Reflection;
using Hangfire.Logging;
using LgymApi.Application;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LgymApi.UnitTests;

[TestFixture]
[NonParallelizable]
public sealed class RuntimeCompositionValidationTests
{
    private static readonly MethodInfo ResolveLogProvider = typeof(LogProvider).GetMethod(
        "ResolveLogProvider",
        BindingFlags.Static | BindingFlags.NonPublic)!;

    [TestCase(true)]
    [TestCase(false)]
    public void ClosedHostComposition_BuildsValidScopesAndResolvesEveryOwnedRuntimeContract(bool isTesting)
    {
        var services = TestServiceProviderFactory.CreateServiceCollection(
            CompositionRootTestHost.CreateFactoryComposition(CreateConfiguration(isTesting), isTesting),
            services =>
            {
                services.AddHttpContextAccessor();
                services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
                foreach (var controllerType in GetApiControllerTypes())
                {
                    services.AddTransient(controllerType);
                }
            });

        AssertPublicFacadeMethods();
        ValidateModuleDescriptorCardinality(services);
        AssertApiAdapterAndIntegrationBoundaries(services);

        WithRestoredHangfireLogProvider(services, provider =>
        {
            using var scope = provider.CreateScope();
            ResolveEveryRegisteredModuleContract(scope.ServiceProvider, services);
            ResolveEveryApiController(scope.ServiceProvider);
        });
    }

    [Test]
    public void DescriptorCardinality_RejectsDuplicateSingleValueDescriptor()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDuplicateDescriptorFixture, DuplicateDescriptorFixture>();
        services.AddScoped<IDuplicateDescriptorFixture, DuplicateDescriptorFixture>();

        var action = () => ValidateModuleDescriptorCardinality(services);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape($"*{typeof(IDuplicateDescriptorFixture).FullName}*exactly once*2*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void RuntimeComposition_RegistersSingleCommandOutboxWriter()
    {
        var services = TestServiceProviderFactory.CreateServiceCollection(
            CompositionRootTestHost.CreateFactoryComposition(CreateConfiguration(isTesting: true), isTesting: true),
            services =>
            {
                services.AddHttpContextAccessor();
                services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
            });
        var assertedValue151 = (services
                    .Where(service => service.ServiceType == typeof(ICommandOutboxWriter))).ToArray();
        Assert.That(assertedValue151, Has.Exactly(1).Items);
        var descriptor = assertedValue151.Single();

        Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(CommandOutboxWriter)));
        WithRestoredHangfireLogProvider(services, provider =>
        {
            using var scope = provider.CreateScope();
            var assertedValue152 = (scope.ServiceProvider.GetServices<ICommandOutboxWriter>()).ToArray();
            Assert.That(assertedValue152, Has.Exactly(1).Items);
            Assert.That(assertedValue152.Single(), Is.TypeOf<CommandOutboxWriter>());
        });
    }

    [Test]
    public void ServiceProviderValidation_RejectsScopedContractConsumedBySingleton()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedFixture>();
        services.AddSingleton<SingletonConsumesScopedFixture>();

        var action = () => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.That(() => { action(); }, Throws.InstanceOf<AggregateException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*Cannot consume scoped service*from singleton*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private static IConfiguration CreateConfiguration(bool isTesting)
    {
        if (isTesting)
        {
            return CompositionRootTestHost.CreateConfiguration();
        }

        var values = TestConfigurationBuilder.ToDictionary(CompositionRootTestHost.CreateConfiguration());
        values["PhotoStorage:Provider"] = "CloudflareR2";
        values["PhotoStorage:BucketName"] = "issue-395-runtime-composition";
        values["PhotoStorage:Endpoint"] = "https://example.r2.cloudflarestorage.com";
        values["PhotoStorage:AccessKeyId"] = "test-access-key";
        values["PhotoStorage:SecretAccessKey"] = "test-secret-key";
        return TestConfigurationBuilder.BuildConfiguration(values);
    }

    private static void WithRestoredHangfireLogProvider(
        IServiceCollection services,
        Action<IServiceProvider> assertion)
    {
        var originalLogProvider = (ILogProvider)ResolveLogProvider.Invoke(null, null)!;
        try
        {
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            assertion(provider);
        }
        finally
        {
            LogProvider.SetCurrentLogProvider(originalLogProvider);
        }
    }

    private static void AssertPublicFacadeMethods()
    {
        var facades = new[]
        {
            new FacadeMethodExpectation(typeof(LgymApi.Platform.PlatformModule), "AddPlatformModule"),
            new FacadeMethodExpectation(typeof(LgymApi.Identity.IdentityModule), "AddIdentityModule"),
            new FacadeMethodExpectation(typeof(LgymApi.TrainingPlanning.TrainingPlanningModule), "AddTrainingPlanningModule"),
            new FacadeMethodExpectation(typeof(LgymApi.Application.Notifications.NotificationsModule), "AddNotificationsModule"),
            new FacadeMethodExpectation(typeof(LgymApi.Application.Reporting.ServiceCollectionExtensions), "AddReportingModule"),
            new FacadeMethodExpectation(typeof(LgymApi.Application.Reporting.ServiceCollectionExtensions), "AddReportingApiAdapters"),
            new FacadeMethodExpectation(typeof(LgymApi.Application.ServiceCollectionExtensions), "AddApplication"),
            new FacadeMethodExpectation(typeof(ApplicationApiAdapterServiceCollectionExtensions), "AddApplicationApiAdapters"),
            new FacadeMethodExpectation(typeof(LgymApi.Application.Notifications.NotificationsModule), "AddNotificationsApiAdapters"),
            new FacadeMethodExpectation(typeof(LgymApi.BackgroundWorker.ServiceProvider), "AddBackgroundWorkerServices")
        };

        foreach (var facade in facades)
        {
            Assert.That((facade.Type.GetMethods(BindingFlags.Public | BindingFlags.Static)).Any(method => method.Name == facade.MethodName), Is.True, $"the closed composition facade '{facade.Type.FullName}' must publish '{facade.MethodName}'");
        }
    }

    private static void ValidateModuleDescriptorCardinality(IServiceCollection services)
    {
        var moduleDescriptors = services
            .Where(descriptor => IsModuleServiceType(descriptor.ServiceType))
            .GroupBy(descriptor => descriptor.ServiceType)
            .ToArray();
        var expectedActionHandlerCounts = CommandContractRegistry.CreateDefault().Contracts
            .ToDictionary(
                contract => typeof(IBackgroundAction<>).MakeGenericType(contract.RuntimeType),
                contract => contract.ExpectedHandlerTypes.Count);
        var approvedCollections = new Dictionary<Type, int>
        {
            [typeof(LgymApi.Application.WorkoutProgress.Scoring.Elo.IExerciseEloCalculator)] = 4,
            [typeof(LgymApi.BackgroundWorker.Common.Notifications.IEmailTemplateComposer)] = 7,
            [typeof(IAccountAccessOwnerCapabilityExtension)] = 3,
            [typeof(LgymApi.Application.Mapping.Core.IMappingProfile)] = 49
        };

        foreach (var group in moduleDescriptors)
        {
            var expectedCount = expectedActionHandlerCounts.TryGetValue(group.Key, out var actionHandlerCount)
                ? actionHandlerCount
                : approvedCollections.TryGetValue(group.Key, out var collectionCount)
                    ? collectionCount
                    : 1;

            if (group.Count() != expectedCount)
            {
                var expectedDescription = expectedCount == 1
                    ? "exactly once"
                    : $"exactly {expectedCount} time(s)";
                throw new InvalidOperationException(
                    $"Service '{group.Key.FullName}' must be registered {expectedDescription}; actual count is {group.Count()}.");
            }
        }

        foreach (var expectedCollection in approvedCollections)
        {
            var actualCount = moduleDescriptors.SingleOrDefault(group => group.Key == expectedCollection.Key)?.Count() ?? 0;
            if (actualCount != expectedCollection.Value)
            {
                throw new InvalidOperationException(
                    $"Approved collection '{expectedCollection.Key.FullName}' must contain exactly {expectedCollection.Value} descriptors; actual count is {actualCount}.");
            }
        }

        foreach (var expectedActionHandler in expectedActionHandlerCounts)
        {
            var actualCount = moduleDescriptors.SingleOrDefault(group => group.Key == expectedActionHandler.Key)?.Count() ?? 0;
            if (actualCount != expectedActionHandler.Value)
            {
                throw new InvalidOperationException(
                    $"Background action contract '{expectedActionHandler.Key.FullName}' must contain exactly {expectedActionHandler.Value} descriptors; actual count is {actualCount}.");
            }
        }
    }

    private static void ResolveEveryRegisteredModuleContract(
        IServiceProvider serviceProvider,
        IServiceCollection services)
    {
        var serviceTypes = services
            .Where(descriptor => IsModuleServiceType(descriptor.ServiceType)
                                 && !descriptor.ServiceType.ContainsGenericParameters)
            .Select(descriptor => descriptor.ServiceType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        foreach (var serviceType in serviceTypes)
        {
            var resolved = serviceProvider.GetServices(serviceType).Cast<object>().ToArray();
            Assert.That(resolved, Is.Not.Empty, serviceType.FullName);
        }
    }

    private static void ResolveEveryApiController(IServiceProvider serviceProvider)
    {
        var controllerTypes = GetApiControllerTypes();

        Assert.That(controllerTypes, Has.Exactly(37).Items);
        Assert.That(controllerTypes, Does.Contain(typeof(LgymApi.Api.Features.User.Controllers.AccountRecoveryController)));
        foreach (var controllerType in controllerTypes)
        {
            Assert.That(serviceProvider.GetRequiredService(controllerType), Is.InstanceOf<ControllerBase>(), controllerType.FullName);
        }
    }

    private static Type[] GetApiControllerTypes()
        => typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static bool IsModuleServiceType(Type serviceType)
        => serviceType.Namespace?.StartsWith("LgymApi.", StringComparison.Ordinal) == true
           && serviceType.Assembly.GetName().Name?.StartsWith("LgymApi.", StringComparison.Ordinal) == true;

    private static void AssertApiAdapterAndIntegrationBoundaries(IServiceCollection services)
    {
        var applicationAdapters = services.Where(descriptor => descriptor.ServiceType.IsPublic
                && descriptor.ServiceType.IsInterface
                && IsApplicationApiAdapterNamespace(descriptor.ServiceType.Namespace))
            .ToArray();
        var notificationsAdapters = services.Where(descriptor => descriptor.ServiceType.IsPublic
                && descriptor.ServiceType.IsInterface
                && descriptor.ServiceType.Namespace == "LgymApi.Notifications.ApiAdapters")
            .ToArray();

        AssertExactScopedInternalAdapters(applicationAdapters, expectedCount: 27, "Application and Reporting API adapters");
        AssertExactScopedInternalAdapters(notificationsAdapters, expectedCount: 3, "Notifications API adapters");

        var retainedIntegrationContracts = new HashSet<string>(StringComparer.Ordinal)
        {
            "LgymApi.Application.Identity.Contracts.Sessions.IAccountSessionDisassociationPort",
            "LgymApi.Application.Notifications.Contracts.Events.ICoachingEmailNotificationFeature",
            "LgymApi.Application.Notifications.Contracts.Events.ICoachingEmailNotificationScheduler",
            "LgymApi.Application.Features.PasswordReset.Contracts.IPasswordRecoveryEmailScheduler"
        };
        var integrationAdapters = services.Where(descriptor => retainedIntegrationContracts.Contains(descriptor.ServiceType.FullName!)).ToArray();

        Assert.That(integrationAdapters, Has.Exactly(4).Items);
        Assert.That((integrationAdapters.GroupBy(descriptor => descriptor.ServiceType)).All(group => group.Count() == 1), Is.True);
        Assert.That((integrationAdapters).All(descriptor => descriptor.Lifetime == ServiceLifetime.Scoped), Is.True);
        Assert.That(integrationAdapters.Select(descriptor => descriptor.ImplementationType).Distinct(), Has.Exactly(3).Items);
        Assert.That(integrationAdapters.Select(descriptor => descriptor.ImplementationType!.Name), Is.EquivalentTo(new[] { "PushInstallationSessionDisassociationAdapter", "CoachingEmailNotificationSchedulerAdapter", "CoachingEmailNotificationSchedulerAdapter", "PasswordRecoveryEmailSchedulerAdapter" }));
    }

    private static bool IsApplicationApiAdapterNamespace(string? namespaceName)
        => namespaceName is not null
           && (namespaceName.StartsWith("LgymApi.Application.Identity.ApiAdapters", StringComparison.Ordinal)
               || namespaceName.StartsWith("LgymApi.Application.TrainingPlanning.ApiAdapters", StringComparison.Ordinal)
               || namespaceName.StartsWith("LgymApi.Application.Coaching.ApiAdapters", StringComparison.Ordinal)
               || namespaceName.StartsWith("LgymApi.Application.Nutrition.ApiAdapters", StringComparison.Ordinal)
               || namespaceName.StartsWith("LgymApi.Application.Platform.ReferenceData.ApiAdapters", StringComparison.Ordinal)
               || namespaceName.StartsWith("LgymApi.Application.WorkoutProgress.ApiAdapters", StringComparison.Ordinal)
               || namespaceName.StartsWith("LgymApi.Application.Reporting.ApiAdapters", StringComparison.Ordinal));

    private static void AssertExactScopedInternalAdapters(
        IReadOnlyCollection<ServiceDescriptor> descriptors,
        int expectedCount,
        string boundary)
    {
        Assert.That(descriptors, Has.Exactly(expectedCount).Items, boundary);
        Assert.That((descriptors.GroupBy(descriptor => descriptor.ServiceType)).All(group => group.Count() == 1), Is.True, boundary);
        Assert.That((descriptors).All(descriptor => descriptor.Lifetime == ServiceLifetime.Scoped), Is.True, boundary);
        Assert.That((descriptors).All(descriptor => descriptor.ImplementationType != null && descriptor.ImplementationType.IsNotPublic), Is.True, boundary);
    }

    private sealed record FacadeMethodExpectation(Type Type, string MethodName);

    private interface IDuplicateDescriptorFixture;

    private sealed class DuplicateDescriptorFixture : IDuplicateDescriptorFixture;

    private sealed class ScopedFixture;

    private sealed class SingletonConsumesScopedFixture(ScopedFixture scopedFixture)
    {
        private readonly ScopedFixture _scopedFixture = scopedFixture;
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _applicationStarted = new();
        private readonly CancellationTokenSource _applicationStopping = new();
        private readonly CancellationTokenSource _applicationStopped = new();

        public CancellationToken ApplicationStarted => _applicationStarted.Token;

        public CancellationToken ApplicationStopping => _applicationStopping.Token;

        public CancellationToken ApplicationStopped => _applicationStopped.Token;

        public void StopApplication()
        {
            _applicationStopping.Cancel();
            _applicationStopped.Cancel();
        }

        public void Dispose()
        {
            _applicationStarted.Dispose();
            _applicationStopping.Dispose();
            _applicationStopped.Dispose();
        }
    }
}
