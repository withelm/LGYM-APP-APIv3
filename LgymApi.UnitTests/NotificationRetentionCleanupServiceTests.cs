using LgymApi.Application.Notifications;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LgymApi.Identity.Contracts.AccountDeletion;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class NotificationRetentionCleanupServiceTests
{
    [Test]
    public async Task CleanupAsync_WhenPushMessageCandidatesAreEmpty_LogsSanitizedStartAndSuccessWithOperationalFields()
    {
        await using var database = CreateDatabase();
        var repository = new PushNotificationMessageRepository(database);
        var unitOfWork = new RecordingUnitOfWork(database);
        var logger = new CapturingLogger<PushNotificationMessageRetentionCleanupService>();
        var service = new PushNotificationMessageRetentionCleanupService(repository, unitOfWork, new RetentionSettings(), logger, Substitute.For<IAccountDeletionRetention>());

        var removed = await service.CleanupAsync();

        Assert.That(removed, Is.EqualTo(0));
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
        Assert.That(logger.Entries, Has.Exactly(2).Items);
        var assertedValue131 = (logger.Entries).Where(entry => entry.Message.Contains("started", StringComparison.Ordinal)).ToArray();
        Assert.That(assertedValue131, Has.Exactly(1).Items);
        var start = assertedValue131.Single();
        Assert.That(start.Properties.Keys, Is.SupersetOf(new[] { "Operation", "CutoffUtc", "BatchSize" }));
        var assertedValue132 = (logger.Entries).Where(entry => entry.Message.Contains("completed", StringComparison.Ordinal)).ToArray();
        Assert.That(assertedValue132, Has.Exactly(1).Items);
        var success = assertedValue132.Single();
        Assert.That(success.Properties.Keys, Is.SupersetOf(new[] { "Operation", "CutoffUtc", "DeletedCount", "BatchCount", "Duration" }));
        Assert.That(success.Properties["DeletedCount"], Is.EqualTo(0));
        Assert.That(success.Properties["BatchCount"], Is.EqualTo(0));
    }

    [Test]
    public async Task CleanupAsync_WhenPushMessageCandidatesFillMultipleBatches_CommitsEachBatchAndDoesNotLogPayload()
    {
        await using var database = CreateDatabase();
        database.PushNotificationMessages.AddRange(CreateMessage("retention-secret-payload"), CreateMessage("retention-secret-payload"));
        await database.SaveChangesAsync();
        var repository = new PushNotificationMessageRepository(database);
        var unitOfWork = new RecordingUnitOfWork(database);
        var logger = new CapturingLogger<PushNotificationMessageRetentionCleanupService>();
        var service = new PushNotificationMessageRetentionCleanupService(repository, unitOfWork, new RetentionSettings(BatchSize: 1), logger, Substitute.For<IAccountDeletionRetention>());

        var removed = await service.CleanupAsync();

        Assert.That(removed, Is.EqualTo(2));
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(2));
        Assert.That((await database.PushNotificationMessages.AsNoTracking().CountAsync()), Is.EqualTo(0));
        Assert.That(logger.Entries, Has.Exactly(2).Items);
        Assert.That((logger.Entries.Select(entry => entry.Message)).Any(message => message.Contains("retention-secret-payload", StringComparison.Ordinal)), Is.False);
        var assertedValue133 = (logger.Entries).Where(entry => entry.Message.Contains("completed", StringComparison.Ordinal)).ToArray();
        Assert.That(assertedValue133, Has.Exactly(1).Items);
        var success = assertedValue133.Single();
        Assert.That(success.Properties["DeletedCount"], Is.EqualTo(2));
        Assert.That(success.Properties["BatchCount"], Is.EqualTo(2));
    }

    [Test]
    public async Task CleanupAsync_WhenDisabledInstallationCleanupIsRerun_IsIdempotentAndDoesNotLogToken()
    {
        await using var database = CreateDatabase();
        database.PushInstallations.Add(CreateInstallation("retention-secret-token"));
        await database.SaveChangesAsync();
        var repository = new PushInstallationRepository(database);
        var unitOfWork = new RecordingUnitOfWork(database);
        var logger = new CapturingLogger<DisabledPushInstallationRetentionCleanupService>();
        var service = new DisabledPushInstallationRetentionCleanupService(repository, unitOfWork, new RetentionSettings(), logger, Substitute.For<IAccountDeletionRetention>());

        var firstRunRemoved = await service.CleanupAsync();
        var secondRunRemoved = await service.CleanupAsync();

        Assert.That(firstRunRemoved, Is.EqualTo(1));
        Assert.That(secondRunRemoved, Is.EqualTo(0));
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        Assert.That((await database.PushInstallations.AsNoTracking().CountAsync()), Is.EqualTo(0));
        Assert.That(logger.Entries, Has.Exactly(4).Items);
        Assert.That((logger.Entries.Select(entry => entry.Message)).Any(message => message.Contains("retention-secret-token", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public async Task CleanupAsync_WhenInAppNotificationBatchCommitFails_PropagatesFailureWithoutContinuing()
    {
        await using var database = CreateDatabase();
        database.InAppNotifications.Add(CreateNotification());
        await database.SaveChangesAsync();
        var repository = new InAppNotificationRepository(database);
        var unitOfWork = new RecordingUnitOfWork(database, new InvalidOperationException("retention commit failed"));
        var logger = new CapturingLogger<InAppNotificationRetentionCleanupService>();
        var service = new InAppNotificationRetentionCleanupService(repository, unitOfWork, new RetentionSettings(), logger, Substitute.For<IAccountDeletionRetention>());

        var action = async () => await service.CleanupAsync();

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("retention commit failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        Assert.That((await database.InAppNotifications.AsNoTracking().CountAsync()), Is.EqualTo(1));
        Assert.That(logger.Entries, Has.Exactly(2).Items);
        var assertedValue134 = (logger.Entries).Where(entry => entry.LogLevel == LogLevel.Error).ToArray();
        Assert.That(assertedValue134, Has.Exactly(1).Items);
        var failure = assertedValue134.Single();
        Assert.That(failure.Exception, Is.TypeOf<InvalidOperationException>());
        Assert.That(failure.Properties.Keys, Is.SupersetOf(new[] { "Operation", "CutoffUtc", "DeletedCount", "BatchCount", "Duration" }));
        Assert.That(failure.Properties["DeletedCount"], Is.EqualTo(0));
        Assert.That(failure.Properties["BatchCount"], Is.EqualTo(0));
        Assert.That(failure.Message, Does.Not.Contain("retention-secret-message"));
    }

    private static AppDbContext CreateDatabase()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"retention-cleanup-{Id<NotificationRetentionCleanupServiceTests>.New():N}")
            .Options);

    private static PushNotificationMessage CreateMessage(string payload)
        => new()
        {
            Id = Id<PushNotificationMessage>.New(),
            UserId = Id<User>.New(),
            PushInstallationId = Id<PushInstallation>.New(),
            SchemaVersion = 1,
            Type = "retention.test",
            EventId = "retention-event",
            PayloadJson = payload,
            Status = PushNotificationStatus.Sent,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-31)
        };

    private static PushInstallation CreateInstallation(string token)
        => new()
        {
            Id = Id<PushInstallation>.New(),
            InstallationId = "retention-installation",
            Platform = "android",
            FcmToken = token,
            Environment = "test",
            DisabledAt = DateTimeOffset.UtcNow.AddDays(-31),
            DisabledReason = "InactiveStale"
        };

    private static InAppNotification CreateNotification()
        => new()
        {
            Id = Id<InAppNotification>.New(),
            RecipientId = Id<User>.New(),
            Type = InAppNotificationTypes.InvitationSent,
            DeliveryKey = "retention-key",
            Message = "retention-secret-message",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-91)
        };

    private sealed record RetentionSettings(int BatchSize = 50) : INotificationRetentionSettings
    {
        public int MessageHistoryDays => 30;
        public int DisabledInstallationDays => 30;
        public int InAppNotificationDays => 90;
    }

    private sealed class RecordingUnitOfWork(AppDbContext database, Exception? saveException = null) : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return saveException is null
                ? database.SaveChangesAsync(cancellationToken)
                : Task.FromException<int>(saveException);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromException<IUnitOfWorkTransaction>(new NotSupportedException());

        public void DetachEntity<TEntity>(TEntity entity)
            where TEntity : class
            => database.Entry(entity).State = EntityState.Detached;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(
                logLevel,
                formatter(state, exception),
                exception,
                state is IEnumerable<KeyValuePair<string, object?>> properties
                    ? properties.ToDictionary(property => property.Key, property => property.Value)
                    : []));
        }
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);
}
