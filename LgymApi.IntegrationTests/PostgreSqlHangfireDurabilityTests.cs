using System.Collections.Concurrent;
using System.Text.Json;
using ImageMagick;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Hangfire.Storage;
using LgymApi.Application;
using LgymApi.Application.Identity.Contracts.BackgroundCommands;
using LgymApi.Application.Mapping;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure;
using LgymApi.Infrastructure.Data;
using LgymApi.Platform;
using LgymApi.Identity;
using LgymApi.TestUtils;
using LgymApi.TrainingPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DependencyInjectionServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
[NonParallelizable]
public sealed class PostgreSqlHangfireDurabilityTests
{
    private static readonly string[] RecurringJobIds =
    [
        "reliability-committed-intent-dispatch",
        "reporting-expired-photo-upload-cleanup",
        "reporting-recurring-report-assignments",
        "push-stale-installation-cleanup",
        "push-notification-message-retention-cleanup",
        "push-disabled-installation-retention-cleanup",
        "in-app-notification-retention-cleanup"
    ];

    [Test]
    public async Task PostgreSqlHangfire_PersistsRestartRetryAndIdempotentReplayWithoutExternalProviders()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        var emailSender = new TestEmailSender { FailuresRemaining = 1 };
        var pushSender = new TestPushProviderSender();
        var stoppedHost = await HangfireDurabilityHost.CreateAsync(lease.ConnectionString, false, emailSender, pushSender);

        string envelopeId;
        string actionJobId;
        string canonicalCommandId;
        try
        {
            var userId = await SeedUserAsync(stoppedHost.Services);
            canonicalCommandId = await StageAndDispatchUserRegisteredCommandAsync(stoppedHost.Services, userId);
            (envelopeId, actionJobId) = await ReadSingleDispatchedEnvelopeAsync(stoppedHost.Services);

            Assert.That(stoppedHost.Storage.GetType().FullName, Is.EqualTo("Hangfire.PostgreSql.PostgreSqlStorage"));
            Assert.That(actionJobId, Is.EqualTo("1"));
            Assert.That(ReadTimeline(stoppedHost.Storage, actionJobId), Does.Contain("Enqueued"));
        }
        finally
        {
            await stoppedHost.DisposeAsync();
        }

        await using var restartedHost = await HangfireDurabilityHost.CreateAsync(lease.ConnectionString, true, emailSender, pushSender);
        await restartedHost.StartAsync();

        await WaitForStateAsync(restartedHost.Storage, actionJobId, "Succeeded");
        var actionTimeline = ReadTimeline(restartedHost.Storage, actionJobId);
        AssertStateSubsequence(actionTimeline, "Enqueued", "Processing", "Succeeded");

        var notificationId = await ReadSingleNotificationIdAsync(restartedHost.Services);
        var emailJobId = await ReadNotificationJobIdAsync(restartedHost.Services, notificationId);

        Assert.That(emailJobId, Is.EqualTo("2"));
        await WaitForStateAsync(restartedHost.Storage, emailJobId, "Succeeded", TimeSpan.FromSeconds(90));
        var emailTimeline = ReadTimeline(restartedHost.Storage, emailJobId);
        AssertStateSubsequence(emailTimeline, "Enqueued", "Processing", "Failed", "Scheduled", "Processing", "Succeeded");

        var sentNotification = await ReadNotificationAsync(restartedHost.Services, notificationId);
        Assert.That(sentNotification.Status, Is.EqualTo(EmailNotificationStatus.Sent));
        Assert.That(sentNotification.Attempts, Is.EqualTo(2));
        Assert.That(sentNotification.SentAt, Is.Not.Null);
        Assert.That(sentNotification.DeliveredAt, Is.Not.Null);
        Assert.That(emailSender.SentMessages, Has.Exactly(1).Items);
        Assert.That(pushSender.Attempts, Is.Empty);

        var replayActionJobId = await EnqueueActionReplayAsync(restartedHost.Services, envelopeId);
        await WaitForStateAsync(restartedHost.Storage, replayActionJobId, "Succeeded");
        var replayEmailJobId = await EnqueueEmailReplayAsync(restartedHost.Services, notificationId);
        await WaitForStateAsync(restartedHost.Storage, replayEmailJobId, "Succeeded");

        Assert.That(replayActionJobId, Is.EqualTo("3"));
        Assert.That(replayEmailJobId, Is.EqualTo("4"));
        Assert.That((await CountNotificationsAsync(restartedHost.Services)), Is.EqualTo(1));
        Assert.That(emailSender.SentMessages, Has.Exactly(1).Items);
        Assert.That(pushSender.Attempts, Is.Empty);

        Assert.That(canonicalCommandId, Is.EqualTo("LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand"));
        Assert.That(typeof(IActionMessageJob).FullName, Is.EqualTo("LgymApi.BackgroundWorker.Common.Jobs.IActionMessageJob"));
        Assert.That(typeof(IActionMessageJob).GetMethod(nameof(IActionMessageJob.ExecuteAsync))!
            .GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(string) }));
        Assert.That(RecurringJobIds, Is.EqualTo(new[] { "reliability-committed-intent-dispatch", "reporting-expired-photo-upload-cleanup", "reporting-recurring-report-assignments", "push-stale-installation-cleanup", "push-notification-message-retention-cleanup", "push-disabled-installation-retention-cleanup", "in-app-notification-retention-cleanup" }));

        TestContext.Progress.WriteLine(
            "Hangfire durability evidence: storage=Hangfire.PostgreSql.PostgreSqlStorage; " +
            $"command={canonicalCommandId}; job=IActionMessageJob.ExecuteAsync(string); recurring={string.Join(',', RecurringJobIds)}; " +
            $"jobIds={actionJobId},{emailJobId},{replayActionJobId},{replayEmailJobId}; " +
            $"actionTimeline={string.Join('>', actionTimeline)}; emailTimeline={string.Join('>', emailTimeline)}; " +
            $"emailAttempts={sentNotification.Attempts}; durableNotifications=1; capturedEmails={emailSender.SentMessages.Count}; capturedPushes={pushSender.Attempts.Count}.");
    }

    private static async Task<Id<User>> SeedUserAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await TestDataFactory.SeedUserAsync(
            database,
            "hangfire-durability-user",
            "hangfire-durability@example.test",
            "password123");
        await database.SaveChangesAsync();
        return user.Id;
    }

    [Test]
    public async Task PostgreSqlExerciseMedia_ResumesPersistedSourceAfterCallerAndWorkerRestart()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        var storage = new RestartExerciseStorage();
        var operation = Id<CorrelationScope>.New().ToString();
        var gif = Convert.FromHexString("47494638396101000100800000000000ffffff21ff0b4e45545343415045322e30030100000021f904000a0000002c000000000100010000020244010021f90400140000002c00000000010001000002024c01003b");
        string jobId;
        var first = await HangfireDurabilityHost.CreateAsync(lease.ConnectionString, false, new(), new(), storage);
        try
        {
            await using var scope = first.Services.CreateAsyncScope();
            using var caller = new CancellationTokenSource();
            var waiting = scope.ServiceProvider.GetRequiredService<IExerciseMediaProcessingScheduler>().ProcessAsync(operation, gif, caller.Token);
            using var connection = first.Storage.GetConnection();
            var queued = first.Storage.GetMonitoringApi().EnqueuedJobs("default", 0, 10);
            Assert.That(queued, Has.Exactly(1).Items);
            jobId = queued.Single().Key;
            var saved = connection.GetJobData(jobId).Job;
            Assert.That(saved.Type, Is.EqualTo(typeof(LgymApi.BackgroundWorker.Jobs.ExerciseMediaProcessingJob)));
            Assert.That(saved.Args[0], Is.EqualTo(operation));
            Assert.That(((byte[])saved.Args[1]), Is.EqualTo(gif));
            caller.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
            Assert.That(ReadTimeline(first.Storage, jobId), Is.EqualTo(new[] { "Enqueued" }));
            Assert.That(storage.Attempts, Is.EqualTo(0));
        }
        finally { await first.DisposeAsync(); }

        // A fresh provider has no caller task, callback or in-memory upload queue.
        var interrupted = await HangfireDurabilityHost.CreateAsync(lease.ConnectionString, true, new(), new(), storage);
        try
        {
            await interrupted.StartAsync();
            await storage.FirstPublication.Task.WaitAsync(TimeSpan.FromSeconds(45));
            Assert.That(storage.Objects, Has.Exactly(1).Items);
        }
        finally
        {
            // Interrupt after external PUT, before the worker commits Succeeded.
            await interrupted.DisposeAsync();
        }

        await using var recovered = await HangfireDurabilityHost.CreateAsync(lease.ConnectionString, true, new(), new(), storage);
        await recovered.StartAsync();
        await WaitForStateAsync(recovered.Storage, jobId, "Succeeded", TimeSpan.FromSeconds(60));
        string? image;
        using (var connection = recovered.Storage.GetConnection())
            image = JsonSerializer.Deserialize<string>(connection.GetStateData(jobId).Data["Result"]);
        var publication = storage.Objects.Single();
        Assert.That(image, Is.EqualTo("https://exercise-media.test/" + publication.Key));
        var assertedChain13 = publication.Key;
        Assert.That(assertedChain13, Does.StartWith($"exercises/{operation}/"));
        Assert.That(assertedChain13, Does.EndWith("/animation.gif"));
        Assert.That(storage.Attempts, Is.EqualTo(2));
        using (var frames = new MagickImageCollection(publication.Value.Bytes))
            Assert.That(frames.Count, Is.EqualTo(2));

        var client = recovered.Services.GetRequiredService<IBackgroundJobClient>();
        Assert.That(client.Requeue(jobId), Is.True);
        await WaitForStateAsync(recovered.Storage, jobId, "Succeeded", minimumOccurrences: 2);
        Assert.That(storage.Attempts, Is.EqualTo(3));
        Assert.That(storage.Objects, Has.Exactly(1).Items);
        await using (var scope = recovered.Services.CreateAsyncScope())
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => scope.ServiceProvider.GetRequiredService<IExerciseMediaProcessingScheduler>()
                .ProcessAsync(Id<CorrelationScope>.New().ToString(), "GIF89a"u8.ToArray(), deadline.Token));
            Assert.That(storage.Attempts, Is.EqualTo(3), "rejected bytes never reach publication");
        }
        TestContext.Progress.WriteLine("Exercise media durability evidence: input-persisted=true; caller-cancelled=true; provider-restarted=true; interrupted-after-publication=true; replay-same-operation=true; immutable-objects=1; gif-frames=2.");
    }

    private sealed class RestartExerciseStorage : IExerciseMediaStorage
    {
        private int _attempts;
        public bool IsConfigured => true;
        public int Attempts => Volatile.Read(ref _attempts);
        public ConcurrentDictionary<string, (byte[] Bytes, string Mime)> Objects { get; } = new();
        public TaskCompletionSource FirstPublication { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            var published = Objects.GetOrAdd(key, _ => (bytes.ToArray(), contentType));
            Assert.That(published.Bytes, Is.EqualTo(bytes));
            Assert.That(published.Mime, Is.EqualTo(contentType));
            if (attempt == 1)
            {
                FirstPublication.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return "https://exercise-media.test/" + key;
        }
    }

    private static async Task<string> StageAndDispatchUserRegisteredCommandAsync(IServiceProvider services, Id<User> userId)
    {
        await using var scope = services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<CommandContractRegistry>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var canonicalId = registry.DescribeForWrite(typeof(UserRegisteredCommand)).CanonicalId;

        await dispatcher.EnqueueAsync(new UserRegisteredCommand { UserId = userId });
        await scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatcher>()
            .DispatchCommittedIntentsAsync(CancellationToken.None);

        return canonicalId;
    }

    private static async Task<(string EnvelopeId, string JobId)> ReadSingleDispatchedEnvelopeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var envelope = await scope.ServiceProvider.GetRequiredService<AppDbContext>().CommandEnvelopes
            .AsNoTracking()
            .SingleAsync();

        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Pending));
        Assert.That(envelope.DispatchedAt, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(envelope.SchedulerJobId), Is.False);
        return (envelope.Id.ToString(), envelope.SchedulerJobId!);
    }

    private static async Task<string> ReadSingleNotificationIdAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var notification = await scope.ServiceProvider.GetRequiredService<AppDbContext>().NotificationMessages
            .AsNoTracking()
            .SingleAsync();

        return notification.Id.ToString();
    }

    private static async Task<string> ReadNotificationJobIdAsync(IServiceProvider services, string notificationId)
    {
        var parsedNotificationId = ParseNotificationId(notificationId);
        await using var scope = services.CreateAsyncScope();
        var notification = await scope.ServiceProvider.GetRequiredService<AppDbContext>().NotificationMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == parsedNotificationId);

        Assert.That(string.IsNullOrWhiteSpace(notification.SchedulerJobId), Is.False);
        return notification.SchedulerJobId!;
    }

    private static async Task<NotificationMessage> ReadNotificationAsync(IServiceProvider services, string notificationId)
    {
        var parsedNotificationId = ParseNotificationId(notificationId);
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().NotificationMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == parsedNotificationId);
    }

    private static Id<NotificationMessage> ParseNotificationId(string notificationId)
    {
        Assert.That(Id<NotificationMessage>.TryParse(notificationId, out var parsedNotificationId), Is.True);
        return parsedNotificationId;
    }

    private static async Task<int> CountNotificationsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().NotificationMessages.CountAsync();
    }

    private static Task<string> EnqueueActionReplayAsync(IServiceProvider services, string envelopeId)
    {
        using var scope = services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
        return Task.FromResult(client.Enqueue<IActionMessageJob>(job => job.ExecuteAsync(envelopeId)));
    }

    private static Task<string> EnqueueEmailReplayAsync(IServiceProvider services, string notificationId)
    {
        using var scope = services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
        return Task.FromResult(client.Enqueue<IEmailJob>(job => job.ExecuteAsync(notificationId)));
    }

    private static IReadOnlyList<string> ReadTimeline(JobStorage storage, string jobId)
    {
        var details = storage.GetMonitoringApi().JobDetails(jobId);
        Assert.That(details, Is.Not.Null);
        return details!.History.OrderBy(entry => entry.CreatedAt).Select(entry => entry.StateName).ToArray();
    }

    private static async Task WaitForStateAsync(
        JobStorage storage,
        string jobId,
        string expectedState,
        TimeSpan? timeout = null,
        int minimumOccurrences = 1)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(30));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (ReadTimeline(storage, jobId).Count(state => string.Equals(state, expectedState, StringComparison.Ordinal)) >= minimumOccurrences)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.Fail($"Hangfire job {jobId} did not record {minimumOccurrences} occurrences of {expectedState}. Timeline: {string.Join('>', ReadTimeline(storage, jobId))}.");
    }

    private static void AssertStateSubsequence(IReadOnlyList<string> timeline, params string[] expectedStates)
    {
        var expectedIndex = 0;
        foreach (var state in timeline)
        {
            if (expectedIndex < expectedStates.Length && state == expectedStates[expectedIndex])
            {
                expectedIndex++;
            }
        }

        Assert.That(expectedIndex, Is.EqualTo(expectedStates.Length), $"timeline {string.Join('>', timeline)} should contain {string.Join('>', expectedStates)} in order");
    }

    private sealed class HangfireDurabilityHost : IAsyncDisposable
    {
        private readonly DependencyInjectionServiceProvider _provider;
        private readonly IHostedService[] _hostedServices;
        private int _startedServiceCount;

        private HangfireDurabilityHost(DependencyInjectionServiceProvider provider, IHostedService[] hostedServices)
        {
            _provider = provider;
            _hostedServices = hostedServices;
            Storage = provider.GetRequiredService<JobStorage>();
        }

        public IServiceProvider Services => _provider;

        public JobStorage Storage { get; }

        public static async Task<HangfireDurabilityHost> CreateAsync(
            string connectionString,
            bool hostBackgroundServer,
            TestEmailSender emailSender,
            TestPushProviderSender pushSender,
            IExerciseMediaStorage? exerciseStorage = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = connectionString,
                    ["Email:Enabled"] = "true",
                    ["Email:FromAddress"] = "no-reply@hangfire.test",
                    ["Email:SmtpHost"] = "localhost",
                    ["Email:SmtpPort"] = "1025",
                    ["Email:InvitationBaseUrl"] = "https://app.test.local/invitations",
                    ["Email:PasswordRecoveryBaseUrl"] = "https://app.test.local/password-recovery",
                    ["Email:TemplateRootPath"] = Path.Combine(AppContext.BaseDirectory, "EmailTemplates"),
                    ["Email:DefaultCulture"] = "en-US",
                    ["PhotoStorage:LocalDevelopmentSigningKey"] = "test-local-photo-signing-key-32-bytes",
                    ["PushNotifications:SendEnabled"] = "false"
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
            services
                .AddPlatformModule()
                .AddIdentityModule()
                .AddTrainingPlanningModule()
                .AddNotificationsModule(configuration)
                .AddReportingModule()
                .AddApplication()
                .AddInfrastructure(configuration, enableSensitiveLogging: true, isTesting: false, hostBackgroundServer: false);
            services.AddBackgroundWorkerServices(isTesting: false, hostBackgroundServer);
            services.AddScoped<IInAppNotificationPushPublisher, NoOpInAppNotificationPushPublisher>();
            IntegrationHostServiceOverrides.ReplaceExternalEffects(services, emailSender, pushSender);
            if (exerciseStorage is not null) services.AddSingleton(exerciseStorage);

            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            try
            {
                await using (var scope = provider.CreateAsyncScope())
                {
                    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await database.Database.MigrateAsync();
                    var storageOptions = new PostgreSqlStorageOptions
                    {
                        PrepareSchemaIfNecessary = true,
                        StartupConnectionMaxRetries = 0,
                        AllowDegradedModeWithoutStorage = false
                    };
                    _ = new PostgreSqlStorage(
                        new NpgsqlConnectionFactory(connectionString, storageOptions, null),
                        storageOptions);
                    await TestDataFactory.SeedDefaultRolesAsync(database);
                    await database.SaveChangesAsync();
                }

                var hostedServices = provider.GetServices<IHostedService>().ToArray();
                Assert.That(hostedServices.OfType<BackgroundJobServerHostedService>(), Has.Exactly(hostBackgroundServer ? 1 : 0).Items);
                Assert.That(hostedServices, Has.Exactly(hostBackgroundServer ? 1 : 0).Items);
                return new HangfireDurabilityHost(provider, hostedServices);
            }
            catch
            {
                await provider.DisposeAsync();
                throw;
            }
        }

        public async Task StartAsync()
        {
            Assert.That(_hostedServices.OfType<BackgroundJobServerHostedService>(), Has.Exactly(1).Items);
            foreach (var hostedService in _hostedServices)
            {
                await hostedService.StartAsync(CancellationToken.None);
                _startedServiceCount++;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                for (var index = _startedServiceCount - 1; index >= 0; index--)
                    await _hostedServices[index].StopAsync(CancellationToken.None);
            }
            finally { await _provider.DisposeAsync(); }
        }
    }

    private sealed class NoOpInAppNotificationPushPublisher : IInAppNotificationPushPublisher
    {
        public Task PushAsync(InAppNotificationResult notification, CancellationToken ct = default) => Task.CompletedTask;
    }
}
