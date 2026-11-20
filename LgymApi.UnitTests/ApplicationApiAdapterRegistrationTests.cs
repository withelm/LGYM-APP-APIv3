using Hangfire.Logging;
using LgymApi.Application;
using LgymApi.Application.Coaching.ApiAdapters;
using LgymApi.Application.Features.PasswordReset.Contracts;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Identity.Contracts.Sessions;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Adapters;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Nutrition.ApiAdapters;
using LgymApi.Application.Platform.ReferenceData.ApiAdapters;
using LgymApi.Application.Reporting.ApiAdapters;
using LgymApi.Application.Reporting;
using LgymApi.Application.TrainingPlanning.ApiAdapters;
using LgymApi.Application.WorkoutProgress.ApiAdapters;
using LgymApi.Notifications;
using LgymApi.Notifications.ApiAdapters;
using LgymApi.Notifications.Contracts;
using LgymApi.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ApplicationApiAdapterRegistrationTests
{
    private static readonly MethodInfo ResolveLogProvider = typeof(LogProvider).GetMethod(
        "ResolveLogProvider",
        BindingFlags.Static | BindingFlags.NonPublic)!;

    private ILogProvider? _initialLogProvider;

    private static readonly IReadOnlyDictionary<Type, Type> ExpectedApplicationRegistrations =
        new Dictionary<Type, Type>
        {
            [typeof(IAuthenticatedAccountApiAdapter)] = typeof(AuthenticatedAccountApiAdapter),
            [typeof(IAccountAccessApiAdapter)] = typeof(AccountAccessApiAdapter),
            [typeof(IAccountEloApiAdapter)] = typeof(AccountEloApiAdapter),
            [typeof(IAccountExternalLoginApiAdapter)] = typeof(AccountExternalLoginApiAdapter),
            [typeof(IAccountTutorialApiAdapter)] = typeof(AccountTutorialApiAdapter),
            [typeof(IAdminAccountManagementApiAdapter)] = typeof(AdminAccountManagementApiAdapter),
            [typeof(IRoleManagementApiAdapter)] = typeof(RoleManagementApiAdapter),
            [typeof(IPlanAccountApiAdapter)] = typeof(PlanApiAdapter),
            [typeof(IManagedPlanAccountApiAdapter)] = typeof(ManagedPlanApiAdapter),
            [typeof(IDietPlanAccountApiAdapter)] = typeof(DietPlanApiAdapter),
            [typeof(ISupplementationApiAdapter)] = typeof(SupplementationApiAdapter),
            [typeof(IExerciseApiAdapter)] = typeof(ExerciseApiAdapter),
            [typeof(IMainRecordsApiAdapter)] = typeof(MainRecordsApiAdapter),
            [typeof(IAppConfigApiAdapter)] = typeof(AppConfigApiAdapter),
            [typeof(ITrainerInvitationApiPort)] = typeof(TrainerInvitationApiAdapter),
            [typeof(ITrainerDashboardProgressApiPort)] = typeof(TrainerDashboardProgressApiAdapter),
            [typeof(ITrainerTraineeNotesApiPort)] = typeof(TrainerTraineeNotesApiAdapter),
            [typeof(ITraineeNotesApiPort)] = typeof(TraineeNotesApiAdapter),
            [typeof(ITraineeRelationshipApiPort)] = typeof(TraineeRelationshipApiAdapter)
        };

    private static readonly IReadOnlyDictionary<Type, Type> ExpectedReportingRegistrations =
        new Dictionary<Type, Type>
        {
            [typeof(ITrainerReportTemplateApiPort)] = typeof(TrainerReportTemplateApiAdapter),
            [typeof(ITrainerPhotoTypeApiPort)] = typeof(TrainerPhotoTypeApiAdapter),
            [typeof(ITrainerReportRequestApiPort)] = typeof(TrainerReportRequestApiAdapter),
            [typeof(ITraineeReportRequestApiPort)] = typeof(TraineeReportRequestApiAdapter),
            [typeof(ITrainerReportPhotoApiPort)] = typeof(TrainerReportPhotoApiAdapter),
            [typeof(ITraineeReportPhotoApiPort)] = typeof(TraineeReportPhotoApiAdapter),
            [typeof(IRecurringReportAssignmentApiPort)] = typeof(RecurringReportAssignmentApiAdapter),
            [typeof(ITraineeRecurringReportAssignmentApiPort)] = typeof(TraineeRecurringReportAssignmentApiAdapter)
        };

    private static readonly IReadOnlyDictionary<Type, Type> ExpectedNotificationsApiRegistrations =
        new Dictionary<Type, Type>
        {
            [typeof(IInAppNotificationApiAdapter)] = typeof(InAppNotificationApiAdapter),
            [typeof(INotificationEventApiAdapter)] = typeof(NotificationEventApiAdapter),
            [typeof(IPushInstallationApiAdapter)] = typeof(PushInstallationApiAdapter)
        };

    private static readonly IReadOnlyDictionary<Type, Type> ExpectedRegistrations =
        ExpectedApplicationRegistrations.Concat(ExpectedReportingRegistrations).Concat(ExpectedNotificationsApiRegistrations)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    [SetUp]
    public void CaptureHangfireLogProvider()
    {
        _initialLogProvider = (ILogProvider)ResolveLogProvider.Invoke(null, null)!;
    }

    [TearDown]
    public void RestoreHangfireLogProvider()
    {
        LogProvider.SetCurrentLogProvider(_initialLogProvider!);
    }

    [Test]
    public void ApiAdapterFacades_RegisterTheExact19Plus8Plus3ManifestExactlyOnceAsScoped()
    {
        var services = new ServiceCollection();
        services.AddReportingApiAdapters();
        services.AddApplicationApiAdapters();
        services.AddNotificationsApiAdapters();

        AssertExpectedApiAdapters(services);

        var discoveredContracts = new[]
            {
                typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly,
                typeof(LgymApi.Application.Reporting.ServiceCollectionExtensions).Assembly,
                typeof(NotificationReference).Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsPublic && type.IsInterface && IsApiAdapterContractNamespace(type.Namespace))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.That(discoveredContracts, Is.EqualTo(ExpectedRegistrations.Keys.OrderBy(type => type.FullName, StringComparer.Ordinal)));
        Assert.That(ExpectedApplicationRegistrations, Has.Exactly(19).Items);
        Assert.That(ExpectedReportingRegistrations, Has.Exactly(8).Items);
        Assert.That(ExpectedNotificationsApiRegistrations, Has.Exactly(3).Items);
        Assert.That(ExpectedRegistrations, Has.Exactly(30).Items);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HostComposition_Captures_Current_Api_Adapter_And_Integration_Descriptor_Ledgers(bool isTesting)
    {
        var configuration = CreateHostConfiguration(isTesting);
        var services = TestServiceProviderFactory.CreateServiceCollection(
            CompositionRootTestHost.CreateFactoryComposition(configuration, isTesting));

        AssertExpectedApiAdapters(services);
        AssertRetainedNotificationsIntegrationAdapters(services);
        AssertKnownMultiRegistrations(services);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        AssertApiAdaptersResolve(scope.ServiceProvider);

        TestContext.Progress.WriteLine(
            $"Issue-395 descriptor baseline: testing={isTesting}; api={ExpectedRegistrations.Count}; "
            + $"application={ExpectedApplicationRegistrations.Count}; notifications={ExpectedNotificationsApiRegistrations.Count}.");
    }

    [Test]
    public void HostCompositionLedgerFixture_Rejects_A_Duplicate_Single_Value_Registration()
    {
        var services = TestServiceProviderFactory.CreateServiceCollection(
            CompositionRootTestHost.CreateFactoryComposition(CompositionRootTestHost.CreateConfiguration()));
        services.AddScoped(
            typeof(LgymApi.Application.Platform.ReferenceData.ApiAdapters.IAppConfigApiAdapter),
            typeof(LgymApi.Application.Platform.ReferenceData.ApiAdapters.AppConfigApiAdapter));

        var action = () => AssertExpectedApiAdapters(services);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*IAppConfigApiAdapter*exactly once*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void HostCompositionLedgerFixture_Rejects_An_Omitted_Owner_Api_Adapter_Registration()
    {
        var services = new ServiceCollection();
        services.AddReportingApiAdapters();
        services.AddApplicationApiAdapters();
        services.AddNotificationsApiAdapters();
        var descriptor = services.Single(candidate => candidate.ServiceType == typeof(ITrainerInvitationApiPort));
        services.Remove(descriptor);

        var action = () => AssertExpectedApiAdapters(services);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*ITrainerInvitationApiPort*exactly once; actual count is 0*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void HostCompositionLedgerFixture_Rejects_A_WrongLifetimeRegistration()
    {
        var services = TestServiceProviderFactory.CreateServiceCollection(
            CompositionRootTestHost.CreateFactoryComposition(CompositionRootTestHost.CreateConfiguration()));
        var descriptor = services.Single(candidate => candidate.ServiceType == typeof(IAppConfigApiAdapter));
        services.Remove(descriptor);
        services.AddSingleton<IAppConfigApiAdapter, AppConfigApiAdapter>();

        var action = () => AssertExpectedApiAdapters(services);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*IAppConfigApiAdapter*lifetime 'Scoped'*Singleton*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void NotificationsModule_RetainsTheThreeIntegrationAdaptersWithTheirExactOwnerContracts()
    {
        var services = new ServiceCollection();
        services.AddNotificationsModule();

        AssertRetainedNotificationsIntegrationAdapters(services);
        Assert.That(typeof(PushInstallationSessionDisassociationAdapter).GetInterfaces(), Is.EquivalentTo(new[] { typeof(IAccountSessionDisassociationPort) }));
        Assert.That(typeof(CoachingEmailNotificationSchedulerAdapter).GetInterfaces(), Is.EquivalentTo(new[] { typeof(ICoachingEmailNotificationFeature), typeof(ICoachingEmailNotificationScheduler) }));
        Assert.That(typeof(PasswordRecoveryEmailSchedulerAdapter).GetInterfaces(), Is.EquivalentTo(new[] { typeof(IPasswordRecoveryEmailScheduler) }));
    }

    private static void AssertExpectedApiAdapters(IServiceCollection services)
    {
        foreach (var expected in ExpectedRegistrations)
        {
            var descriptors = services.Where(candidate => candidate.ServiceType == expected.Key).ToArray();
            if (descriptors.Length != 1)
            {
                throw new InvalidOperationException($"API adapter '{expected.Key.FullName}' must be registered exactly once; actual count is {descriptors.Length}.");
            }

            if (descriptors[0].Lifetime != ServiceLifetime.Scoped)
            {
                throw new InvalidOperationException(
                    $"API adapter '{expected.Key.FullName}' must use lifetime '{ServiceLifetime.Scoped}'; actual lifetime is '{descriptors[0].Lifetime}'.");
            }

            Assert.That(descriptors[0].ImplementationType, Is.EqualTo(expected.Value), expected.Key.FullName);
            Assert.That(descriptors[0].ImplementationType!.IsNotPublic, Is.True, expected.Key.FullName);
        }
    }

    private static void AssertRetainedNotificationsIntegrationAdapters(IServiceCollection services)
    {
        AssertSingle(services, typeof(IAccountSessionDisassociationPort), typeof(PushInstallationSessionDisassociationAdapter));
        AssertSingle(services, typeof(ICoachingEmailNotificationFeature), typeof(CoachingEmailNotificationSchedulerAdapter));
        AssertSingle(services, typeof(ICoachingEmailNotificationScheduler), typeof(CoachingEmailNotificationSchedulerAdapter));
        AssertSingle(services, typeof(IPasswordRecoveryEmailScheduler), typeof(PasswordRecoveryEmailSchedulerAdapter));
    }

    private static void AssertApiAdaptersResolve(IServiceProvider serviceProvider)
    {
        foreach (var expected in ExpectedRegistrations)
        {
            Assert.That(serviceProvider.GetRequiredService(expected.Key).GetType(), Is.EqualTo(expected.Value), expected.Key.FullName);
        }

        Assert.That(serviceProvider.GetRequiredService<IAccountSessionDisassociationPort>(), Is.TypeOf<PushInstallationSessionDisassociationAdapter>());
        Assert.That(serviceProvider.GetRequiredService<ICoachingEmailNotificationFeature>(), Is.TypeOf<CoachingEmailNotificationSchedulerAdapter>());
        Assert.That(serviceProvider.GetRequiredService<ICoachingEmailNotificationScheduler>(), Is.TypeOf<CoachingEmailNotificationSchedulerAdapter>());
        Assert.That(serviceProvider.GetRequiredService<IPasswordRecoveryEmailScheduler>(), Is.TypeOf<PasswordRecoveryEmailSchedulerAdapter>());
    }

    private static void AssertKnownMultiRegistrations(IServiceCollection services)
    {
        Assert.That(services.Count(descriptor => descriptor.ServiceType.FullName == "LgymApi.Application.WorkoutProgress.Scoring.Elo.IExerciseEloCalculator"), Is.EqualTo(4));
        Assert.That(services.Count(descriptor => descriptor.ServiceType.FullName == "LgymApi.BackgroundWorker.Common.Notifications.IEmailTemplateComposer"), Is.EqualTo(7));
        Assert.That(services.Count(descriptor => descriptor.ServiceType.FullName == "LgymApi.Application.Mapping.Core.IMappingProfile"), Is.EqualTo(49));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IMappingProfile)
            && descriptor.ImplementationType?.FullName == "LgymApi.Application.Notifications.Email.AccountRecoveryEmailMappingProfile"), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IMappingProfile)
            && descriptor.ImplementationType?.FullName == "LgymApi.Application.Coaching.ApiAdapters.CoachingApiAdapterMappingProfile"), Is.EqualTo(1));
        Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(IMappingProfile)
            && descriptor.ImplementationType?.FullName == "LgymApi.Application.Reporting.ApiAdapters.ReportingApiAdapterMappingProfile"), Is.EqualTo(1));
    }

    private static void AssertSingle(IServiceCollection services, Type serviceType, Type implementationType)
    {
        var descriptors = services.Where(descriptor => descriptor.ServiceType == serviceType).ToArray();
        Assert.That(descriptors, Has.Length.EqualTo(1), serviceType.FullName);
        Assert.That(descriptors[0].Lifetime, Is.EqualTo(ServiceLifetime.Scoped), serviceType.FullName);
        Assert.That(descriptors[0].ImplementationType, Is.EqualTo(implementationType), serviceType.FullName);
    }

    private static Microsoft.Extensions.Configuration.IConfiguration CreateHostConfiguration(bool isTesting)
    {
        if (isTesting)
        {
            return CompositionRootTestHost.CreateConfiguration();
        }

        var values = TestConfigurationBuilder.ToDictionary(CompositionRootTestHost.CreateConfiguration());
        values["PhotoStorage:Provider"] = "CloudflareR2";
        values["PhotoStorage:BucketName"] = "issue-395-descriptor-ledger";
        values["PhotoStorage:Endpoint"] = "https://example.r2.cloudflarestorage.com";
        values["PhotoStorage:AccessKeyId"] = "test-access-key";
        values["PhotoStorage:SecretAccessKey"] = "test-secret-key";
        return TestConfigurationBuilder.BuildConfiguration(values);
    }

    private static bool IsApiAdapterContractNamespace(string? namespaceName)
        => namespaceName is not null
            && (namespaceName.StartsWith("LgymApi.Application.Identity.ApiAdapters", StringComparison.Ordinal)
                 || namespaceName.StartsWith("LgymApi.Application.TrainingPlanning.ApiAdapters", StringComparison.Ordinal)
                 || namespaceName.StartsWith("LgymApi.Application.Coaching.ApiAdapters", StringComparison.Ordinal)
                 || namespaceName.StartsWith("LgymApi.Application.Nutrition.ApiAdapters", StringComparison.Ordinal)
                 || namespaceName.StartsWith("LgymApi.Application.Platform.ReferenceData.ApiAdapters", StringComparison.Ordinal)
                  || namespaceName.StartsWith("LgymApi.Application.WorkoutProgress.ApiAdapters", StringComparison.Ordinal)
                  || namespaceName.StartsWith("LgymApi.Notifications.ApiAdapters", StringComparison.Ordinal)
                 || namespaceName.StartsWith("LgymApi.Application.Reporting.ApiAdapters", StringComparison.Ordinal));
}
