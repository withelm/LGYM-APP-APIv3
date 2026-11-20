using System.Reflection;
using Hangfire;
using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.States;
using Hangfire.Storage;
using LgymApi.Api;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Jobs;
using WorkerHangfirePushBackgroundScheduler = LgymApi.BackgroundWorker.Services.HangfirePushBackgroundScheduler;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Services;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.Core;
using Newtonsoft.Json;
using EmailJob = LgymApi.Infrastructure.Jobs.EmailJob;
using InvitationEmailJob = LgymApi.Infrastructure.Jobs.InvitationEmailJob;
using WelcomeEmailJob = LgymApi.Infrastructure.Jobs.WelcomeEmailJob;

namespace LgymApi.UnitTests;

[TestFixture]
[NonParallelizable]
public sealed class HangfireContractCompatibilityTests
{
    private static readonly MethodInfo ResolveLogProvider = typeof(LogProvider).GetMethod(
        "ResolveLogProvider",
        BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly PersistedJobContract[] PersistedJobs =
    [
        new("ActionMessage", typeof(IActionMessageJob), typeof(ActionMessageJob), "LgymApi.BackgroundWorker.Jobs", [typeof(string)], []),
        new("Email", typeof(IEmailJob), typeof(EmailJob), "LgymApi.Infrastructure.Jobs", [typeof(string)], []),
        new("InvitationEmail", typeof(IInvitationEmailJob), typeof(InvitationEmailJob), "LgymApi.Infrastructure.Jobs", [typeof(string)], []),
        new("WelcomeEmail", typeof(IWelcomeEmailJob), typeof(WelcomeEmailJob), "LgymApi.Infrastructure.Jobs", [typeof(string)], []),
        new("PushNotification", typeof(IPushNotificationJob), typeof(PushNotificationJob), "LgymApi.BackgroundWorker.Jobs", [typeof(string), typeof(CancellationToken)], [1]),
        new("CommittedIntentDispatch", typeof(ICommittedIntentDispatchJob), typeof(CommittedIntentDispatchJob), "LgymApi.BackgroundWorker.Jobs", [typeof(CancellationToken)], [0]),
        new("ExpiredPhotoUploadCleanup", typeof(IExpiredPhotoUploadCleanupJob), typeof(ExpiredPhotoUploadCleanupJob), "LgymApi.BackgroundWorker.Jobs", [typeof(CancellationToken)], [0]),
        new("RecurringReportAssignmentProcessing", typeof(IRecurringReportAssignmentProcessingJob), typeof(RecurringReportAssignmentProcessingJob), "LgymApi.BackgroundWorker.Jobs", [typeof(CancellationToken)], [0]),
        new("StalePushInstallationCleanup", typeof(IStalePushInstallationCleanupJob), typeof(StalePushInstallationCleanupJob), "LgymApi.BackgroundWorker.Jobs", [typeof(CancellationToken)], [0]),
        new("PushNotificationMessageRetentionCleanup", typeof(IPushNotificationMessageRetentionCleanupJob), typeof(PushNotificationMessageRetentionCleanupJob), "LgymApi.BackgroundWorker.Jobs", [typeof(CancellationToken)], [0]),
        new("DisabledPushInstallationRetentionCleanup", typeof(IDisabledPushInstallationRetentionCleanupJob), typeof(DisabledPushInstallationRetentionCleanupJob), "LgymApi.BackgroundWorker.Jobs", [typeof(CancellationToken)], [0]),
        new("InAppNotificationRetentionCleanup", typeof(IInAppNotificationRetentionCleanupJob), typeof(InAppNotificationRetentionCleanupJob), "LgymApi.BackgroundWorker.Jobs", [typeof(CancellationToken)], [0])
    ];

    private ILogProvider _originalLogProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _originalLogProvider = (ILogProvider)ResolveLogProvider.Invoke(null, null)!;
    }

    [TearDown]
    public void TearDown()
    {
        LogProvider.SetCurrentLogProvider(_originalLogProvider);
    }

    [TestCaseSource(nameof(GetPersistedJobContracts))]
    public void PersistedJobContract_HasFrozenIdentityAndSignature(PersistedJobContract contract)
    {
        Assert.That(contract.ContractType.Assembly.GetName().Name, Is.EqualTo("LgymApi.BackgroundWorker.Common"));
        Assert.That(contract.ContractType.Namespace, Is.EqualTo("LgymApi.BackgroundWorker.Common.Jobs"));
        Assert.That(contract.ContractType.FullName, Is.EqualTo($"LgymApi.BackgroundWorker.Common.Jobs.I{contract.Name}Job"));

        Assert.That(contract.WorkerType.Assembly.GetName().Name, Is.EqualTo("LgymApi.BackgroundWorker"));
        Assert.That(contract.WorkerType.Namespace, Is.EqualTo(contract.WorkerNamespace));
        Assert.That(contract.WorkerType.FullName, Is.EqualTo($"{contract.WorkerNamespace}.{contract.Name}Job"));
        Assert.That(contract.WorkerType.IsSealed, Is.True);
        Assert.That((contract.WorkerType).GetInterfaces(), Does.Contain(contract.ContractType));

        AssertExecuteAsyncSignature(contract.ContractType, contract);
        AssertExecuteAsyncSignature(contract.WorkerType, contract);
    }

    [TestCase(typeof(ActionMessageJob), true)]
    [TestCase(typeof(EmailJob), false)]
    [TestCase(typeof(InvitationEmailJob), false)]
    [TestCase(typeof(WelcomeEmailJob), false)]
    public void PersistedJobContract_RetryPolicyUsesThreeInterruptedRunBackoffs(Type workerType, bool isClassAttribute)
    {
        var attribute = isClassAttribute
            ? workerType.GetCustomAttribute<AutomaticRetryAttribute>()
            : workerType.GetMethod("ExecuteAsync")!.GetCustomAttribute<AutomaticRetryAttribute>();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute!.Attempts, Is.EqualTo(3));
        Assert.That(attribute.DelaysInSeconds, Is.EqualTo(new[] { 60, 300, 900 }));
    }

    [Test]
    public void PersistedJobContract_PushDisablesAutomaticRetry()
    {
        var attribute = typeof(PushNotificationJob).GetCustomAttribute<AutomaticRetryAttribute>();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute!.Attempts, Is.EqualTo(0));
    }

    [TestCase(typeof(EmailJob), 60)]
    [TestCase(typeof(RecurringReportAssignmentProcessingJob), 300)]
    public void PersistedJobContract_ExecutionLockPreventsOverlappingInterruptedRuns(Type workerType, int timeoutInSeconds)
    {
        var method = workerType.GetMethod("ExecuteAsync")!;
        var attribute = method.CustomAttributes.SingleOrDefault(candidate => candidate.AttributeType == typeof(DisableConcurrentExecutionAttribute));

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute!.ConstructorArguments, Has.Exactly(1).Items);
        Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo(timeoutInSeconds));
    }

    [Test]
    public void SchedulerAdapters_CapturePersistedInterfaceExpressionsAndIdOnlyPayloads()
    {
        var client = new CapturingBackgroundJobClient();
        var actionMessageId = Id<CommandEnvelope>.New();
        var notificationId = Id<NotificationMessage>.New();
        var pushNotificationId = Id<PushNotificationMessage>.New();

        new HangfireActionMessageScheduler(client).Enqueue(actionMessageId);
        new HangfireEmailBackgroundScheduler(client).Enqueue(notificationId);
        var pushScheduler = new WorkerHangfirePushBackgroundScheduler(client);
        pushScheduler.Enqueue(pushNotificationId);
        pushScheduler.ScheduleRetry(pushNotificationId, TimeSpan.FromMinutes(5));

        Assert.That(client.CreatedJobs, Has.Exactly(4).Items);
        AssertCapturedJob(client.CreatedJobs[0], typeof(IActionMessageJob), actionMessageId.ToString(), typeof(EnqueuedState));
        AssertCapturedJob(client.CreatedJobs[1], typeof(IEmailJob), notificationId.ToString(), typeof(EnqueuedState));
        AssertCapturedJob(client.CreatedJobs[2], typeof(IPushNotificationJob), pushNotificationId.ToString(), typeof(EnqueuedState), hasCancellationToken: true);
        AssertCapturedJob(client.CreatedJobs[3], typeof(IPushNotificationJob), pushNotificationId.ToString(), typeof(ScheduledState), hasCancellationToken: true);
    }

    [Test]
    public void RecurringJobs_PersistFrozenIdsCronsAndInterfaceTargets()
    {
        var storage = new CapturingJobStorage();
        JobStorage? previousStorage = null;
        try
        {
            previousStorage = JobStorage.Current;
        }
        catch (InvalidOperationException)
        {
        }

        JobStorage.Current = storage;

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "HangfireContract" });
            builder.Services.AddHangfire(_ => { });
            using var app = builder.Build();

            ProgramHangfire.ConfigureRecurringJobs(app, "Testing");
        }
        finally
        {
            if (previousStorage is not null)
            {
                JobStorage.Current = previousStorage;
            }
        }

        var expectedJobs = new[]
        {
            new RecurringJobContract("reliability-committed-intent-dispatch", "* * * * *", typeof(ICommittedIntentDispatchJob)),
            new RecurringJobContract("reporting-expired-photo-upload-cleanup", "* * * * *", typeof(IExpiredPhotoUploadCleanupJob)),
            new RecurringJobContract("reporting-recurring-report-assignments", "* * * * *", typeof(IRecurringReportAssignmentProcessingJob)),
            new RecurringJobContract("push-stale-installation-cleanup", Cron.Daily(3), typeof(IStalePushInstallationCleanupJob)),
            new RecurringJobContract("push-notification-message-retention-cleanup", Cron.Daily(), typeof(IPushNotificationMessageRetentionCleanupJob)),
            new RecurringJobContract("push-disabled-installation-retention-cleanup", Cron.Daily(), typeof(IDisabledPushInstallationRetentionCleanupJob)),
            new RecurringJobContract("in-app-notification-retention-cleanup", Cron.Daily(), typeof(IInAppNotificationRetentionCleanupJob)),
            new RecurringJobContract("identity-account-finalization", "* * * * *", typeof(AccountFinalizationJob)),
            new RecurringJobContract("reporting-account-photo-deletion", "* * * * *", typeof(AccountPhotoDeletionJob))
        };

        Assert.That(storage.RecurringJobHashes, Has.Exactly(9).Items);
        foreach (var expectedJob in expectedJobs)
        {
            var hash = storage.RecurringJobHashes[$"recurring-job:{expectedJob.Id}"];
            Assert.That(hash["Cron"], Is.EqualTo(expectedJob.Cron));

            var job = InvocationData.DeserializePayload(hash["Job"]).DeserializeJob();
            Assert.That(job.Type, Is.EqualTo(expectedJob.ContractType));
            Assert.That(job.Method.Name, Is.EqualTo("ExecuteAsync"));
            Assert.That(job.Method.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(CancellationToken) }));
            var assertedValue111 = (job.Args).ToArray();
            Assert.That(assertedValue111, Has.Exactly(1).Items);
            Assert.That(assertedValue111.Single(), Is.EqualTo(CancellationToken.None));
        }
    }

    [Test]
    public void PlatformComposition_UsesFrozenHangfireCompatibilitySerializerStorageAndServerConditions()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=hangfire-contracts;Username=test;Password=test"
        });

        var testingServices = new ServiceCollection();
        testingServices.AddPlatformServices(configuration, enableSensitiveLogging: false, isTesting: true, hostBackgroundServer: true);
        Assert.That((testingServices).Any(descriptor => descriptor.ServiceType == typeof(JobStorage)), Is.False);
        Assert.That(FindHangfireServerDescriptor(testingServices), Is.Null);

        var clientOnlyServices = new ServiceCollection();
        clientOnlyServices.AddPlatformServices(configuration, enableSensitiveLogging: false, isTesting: false, hostBackgroundServer: false);
        using var clientOnlyProvider = clientOnlyServices.BuildServiceProvider();

        var runtimeStorage = clientOnlyProvider.GetRequiredService<JobStorage>();
        Assert.That(runtimeStorage.GetType().FullName, Is.EqualTo("Hangfire.PostgreSql.PostgreSqlStorage"));
        var storageOptions = runtimeStorage.GetType()
            .GetProperty("Options", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(runtimeStorage)!;
        var prepareSchemaIfNecessary = (bool)storageOptions.GetType()
            .GetProperty("PrepareSchemaIfNecessary")!
            .GetValue(storageOptions)!;
        Assert.That(prepareSchemaIfNecessary, Is.False);
        Assert.That(FindHangfireServerDescriptor(clientOnlyServices), Is.Null);
        Assert.That(GetCompatibilityLevel(), Is.EqualTo((int)CompatibilityLevel.Version_180));
        Assert.That(GetConfiguredTypeSerializer().DynamicInvoke(typeof(ICommittedIntentDispatchJob)), Is.EqualTo("LgymApi.BackgroundWorker.Common.Jobs.ICommittedIntentDispatchJob, LgymApi.BackgroundWorker.Common"));
        AssertRecommendedSerializerSettings(GetConfiguredSerializerSettings());

        var serverServices = new ServiceCollection();
        serverServices.AddPlatformServices(configuration, enableSensitiveLogging: false, isTesting: false, hostBackgroundServer: true);
        var serverDescriptor = FindHangfireServerDescriptor(serverServices);
        Assert.That(serverDescriptor, Is.Not.Null);
        Assert.That(serverDescriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Transient));
    }

    [TestCase(typeof(IActionMessageJob), "LgymApi.Domain.ValueObjects.Id`1[[LgymApi.Domain.Entities.CommandEnvelope, LgymApi.Domain]], LgymApi.Domain")]
    [TestCase(typeof(IEmailJob), "LgymApi.Domain.ValueObjects.Id`1[[LgymApi.Domain.Entities.NotificationMessage, LgymApi.Domain]], LgymApi.Domain")]
    [TestCase(typeof(IInvitationEmailJob), "LgymApi.Domain.ValueObjects.Id`1[[LgymApi.Domain.Entities.NotificationMessage, LgymApi.Domain]], LgymApi.Domain")]
    [TestCase(typeof(IWelcomeEmailJob), "LgymApi.Domain.ValueObjects.Id`1[[LgymApi.Domain.Entities.NotificationMessage, LgymApi.Domain]], LgymApi.Domain")]
    [TestCase(typeof(IPushNotificationJob), "LgymApi.Domain.ValueObjects.Id`1[[LgymApi.Domain.Entities.PushNotificationMessage, LgymApi.Domain]], LgymApi.Domain")]
    public void LegacyInvocationData_ReplaysAllowlistedTypedIdAsCurrentString(Type contractType, string legacyParameterType)
    {
        ConfigureHangfireTypeResolver();
        const string uuid = "11111111-1111-1111-1111-111111111111";
        var parameterTypes = contractType == typeof(IPushNotificationJob)
            ? new[] { legacyParameterType, "System.Threading.CancellationToken, System.Private.CoreLib" }
            : new[] { legacyParameterType };
        var arguments = contractType == typeof(IPushNotificationJob)
            ? new[] { JsonConvert.SerializeObject(uuid), JsonConvert.SerializeObject(CancellationToken.None) }
            : new[] { JsonConvert.SerializeObject(uuid) };
        var invocation = new InvocationData(
            $"{contractType.FullName}, {contractType.Assembly.GetName().Name}",
            "ExecuteAsync",
            JsonConvert.SerializeObject(parameterTypes),
            JsonConvert.SerializeObject(arguments));

        var job = invocation.DeserializeJob();

        Assert.That(job.Type, Is.EqualTo(contractType));
        var expectedParameterTypes = contractType == typeof(IPushNotificationJob)
            ? new[] { typeof(string), typeof(CancellationToken) }
            : new[] { typeof(string) };
        Assert.That(job.Method.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(expectedParameterTypes));
        Assert.That(job.Args[0], Is.EqualTo(uuid));
    }

    [TestCase("LgymApi.Domain.ValueObjects.Id`1[[LgymApi.Domain.Entities.User, LgymApi.Domain]], LgymApi.Domain")]
    [TestCase("not-a-type")]
    public void LegacyInvocationData_RejectsUnapprovedOrMalformedParameterMetadata(string parameterType)
    {
        ConfigureHangfireTypeResolver();
        var invocation = new InvocationData(
            "LgymApi.BackgroundWorker.Common.Jobs.IEmailJob, LgymApi.BackgroundWorker.Common",
            "ExecuteAsync",
            JsonConvert.SerializeObject(new[] { parameterType }),
            JsonConvert.SerializeObject(new[] { JsonConvert.SerializeObject("11111111-1111-1111-1111-111111111111") }));

        Action action = () => invocation.DeserializeJob();

        Assert.That(() => { action(); }, Throws.InstanceOf<Exception>());
    }

    private static void ConfigureHangfireTypeResolver()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=hangfire-replay;Username=test;Password=test"
        });
        new ServiceCollection().AddPlatformServices(configuration, enableSensitiveLogging: false, isTesting: false, hostBackgroundServer: false);
        InvocationData.SetTypeResolver(LgymApi.Infrastructure.ServiceCollectionExtensions.ResolvePersistedJobType);
    }

    [Test]
    public void WorkerFacade_DelegatesHostedServerRegistrationToInfrastructure()
    {
        var configuration = TestConfigurationBuilder.BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=hangfire-worker-host;Username=test;Password=test"
        });
        var services = new ServiceCollection();
        services.AddPlatformServices(configuration, enableSensitiveLogging: false, isTesting: false, hostBackgroundServer: false);

        services.AddBackgroundWorkerServices(isTesting: false, hostBackgroundServer: true);

        var serverDescriptor = FindHangfireServerDescriptor(services);
        Assert.That(serverDescriptor, Is.Not.Null);
        Assert.That(serverDescriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Transient));
    }

    [Test]
    public void HangfireServerDescriptor_DoesNotTreatUnrelatedHostedServiceAsHangfireServer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService, UnrelatedHostedService>();

        Assert.That((services).Where(descriptor => descriptor.ServiceType == typeof(IHostedService)), Has.Exactly(1).Items);
        Assert.That(FindHangfireServerDescriptor(services), Is.Null);
    }

    private static IEnumerable<TestCaseData> GetPersistedJobContracts()
        => PersistedJobs.Select(contract => new TestCaseData(contract).SetName($"PersistedJobContract_{contract.Name}_IsFrozen"));

    private static void AssertExecuteAsyncSignature(Type type, PersistedJobContract contract)
    {
        var methods = type.GetMethods().Where(method => method.Name == "ExecuteAsync").ToArray();
        Assert.That(methods, Has.Exactly(1).Items);

        var method = methods[0];
        Assert.That(method.ReturnType, Is.EqualTo(typeof(Task)));
        Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(contract.ParameterTypes));
        Assert.That(method.GetParameters().Select(parameter => parameter.IsOptional), Is.EqualTo(Enumerable.Range(0, contract.ParameterTypes.Length)
                .Select(index => contract.OptionalParameterIndexes.Contains(index))));

        Assert.That((method.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .Select(parameter => parameter.ParameterType)).All(parameterType => parameterType == typeof(string)), Is.True);
    }

    private static void AssertCapturedJob(CapturedBackgroundJob captured, Type expectedContractType, object expectedId, Type expectedStateType, bool hasCancellationToken = false)
    {
        Assert.That(captured.Job.Type, Is.EqualTo(expectedContractType));
        Assert.That(captured.Job.Method.DeclaringType, Is.EqualTo(expectedContractType));
        Assert.That(captured.Job.Method.Name, Is.EqualTo("ExecuteAsync"));
        Assert.That(captured.Job.Args[0], Is.EqualTo(expectedId));
        Assert.That(captured.Job.Args[0].GetType(), Is.EqualTo(expectedId.GetType()));
        Assert.That(captured.Job.Args, Has.Exactly(hasCancellationToken ? 2 : 1).Items);
        if (hasCancellationToken)
        {
            Assert.That(captured.Job.Args[1], Is.EqualTo(CancellationToken.None));
        }

        Assert.That(captured.State.GetType(), Is.EqualTo(expectedStateType));
    }

    private static int GetCompatibilityLevel()
        => (int)typeof(GlobalConfiguration)
            .GetField("_compatibilityLevel", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;

    private static Delegate GetConfiguredTypeSerializer()
        => (Delegate)typeof(Job)
            .Assembly.GetType("Hangfire.Common.TypeHelper")!
            .GetField("_currentTypeSerializer", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;

    private static JsonSerializerSettings GetConfiguredSerializerSettings()
        => (JsonSerializerSettings)typeof(Job)
            .Assembly.GetType("Hangfire.Common.SerializationHelper")!
            .GetField("_userSerializerSettings", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;

    private static void AssertRecommendedSerializerSettings(JsonSerializerSettings settings)
    {
        Assert.That(settings.TypeNameHandling, Is.EqualTo(TypeNameHandling.Auto));
        Assert.That(settings.DefaultValueHandling, Is.EqualTo(DefaultValueHandling.IgnoreAndPopulate));
        Assert.That(settings.NullValueHandling, Is.EqualTo(NullValueHandling.Ignore));
        Assert.That(settings.CheckAdditionalContent, Is.True);
        Assert.That(settings.MaxDepth, Is.EqualTo(128));
        Assert.That(settings.TypeNameAssemblyFormatHandling, Is.EqualTo(TypeNameAssemblyFormatHandling.Simple));
        Assert.That(settings.SerializationBinder!.GetType().FullName, Is.EqualTo("Hangfire.Common.TypeHelperSerializationBinder"));
    }

    private static ServiceDescriptor? FindHangfireServerDescriptor(IServiceCollection services)
        => services.SingleOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationFactory?.Method.ReturnType == typeof(BackgroundJobServerHostedService));

    public sealed record PersistedJobContract(
        string Name,
        Type ContractType,
        Type WorkerType,
        string WorkerNamespace,
        Type[] ParameterTypes,
        int[] OptionalParameterIndexes);

    private sealed record RecurringJobContract(string Id, string Cron, Type ContractType);

    private sealed record CapturedBackgroundJob(Job Job, IState State);

    private sealed class UnrelatedHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingBackgroundJobClient : IBackgroundJobClient
    {
        public List<CapturedBackgroundJob> CreatedJobs { get; } = [];

        public string Create(Job job, IState state)
        {
            CreatedJobs.Add(new CapturedBackgroundJob(job, state));
            return CreatedJobs.Count.ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => throw new NotSupportedException();
    }

    private sealed class CapturingJobStorage : JobStorage
    {
        private readonly IStorageConnection _connection;

        public CapturingJobStorage()
        {
            _connection = Substitute.For<IStorageConnection>();
            var transaction = Substitute.For<IWriteOnlyTransaction>();
            _connection.CreateWriteTransaction().Returns(transaction);
            _connection.GetAllEntriesFromHash(Arg.Any<string>()).Returns(_ => new Dictionary<string, string>());
            transaction.When(call => call.SetRangeInHash(Arg.Any<string>(), Arg.Any<IEnumerable<KeyValuePair<string, string>>>()))
                .Do(CaptureRecurringJobHash);
        }

        public Dictionary<string, Dictionary<string, string>> RecurringJobHashes { get; } = [];

        public override IMonitoringApi GetMonitoringApi() => Substitute.For<IMonitoringApi>();

        public override IStorageConnection GetConnection() => _connection;

        private void CaptureRecurringJobHash(CallInfo call)
        {
            var key = call.ArgAt<string>(0);
            if (key.StartsWith("recurring-job:", StringComparison.Ordinal))
            {
                RecurringJobHashes[key] = call.ArgAt<IEnumerable<KeyValuePair<string, string>>>(1)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }
        }
    }
}
