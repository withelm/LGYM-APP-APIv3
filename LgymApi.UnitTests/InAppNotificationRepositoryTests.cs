using LgymApi.Application.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InAppNotificationRepositoryTests
{
    [Test]
    public async Task FindByDeliveryKeyAsync_AndGetByIdAsync_IgnoreDeletedRows()
    {
        await using var db = CreateDbContext("notification-repo-find");
        var userId = Id<User>.New();
        var type = InAppNotificationTypes.ReportFeedbackReceived;
        var active = CreateNotification(userId, type, "active-key", false, false, DateTimeOffset.UtcNow);
        var deleted = CreateNotification(userId, type, "deleted-key", false, true, DateTimeOffset.UtcNow);
        db.InAppNotifications.AddRange(active, deleted);
        await db.SaveChangesAsync();

        var repository = new InAppNotificationRepository(db);

        Assert.That((await repository.FindByDeliveryKeyAsync(userId, type, "active-key")), Is.Not.Null);
        Assert.That((await repository.FindByDeliveryKeyAsync(userId, type, "deleted-key")), Is.Null);
        Assert.That((await repository.GetByIdAsync(deleted.Id)), Is.Null);
    }

    [Test]
    public async Task GetPageAsync_UsesCursorAndReturnsOneExtraRow()
    {
        await using var db = CreateDbContext("notification-repo-page");
        var userId = Id<User>.New();
        var first = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "1", false, false, DateTimeOffset.UtcNow.AddMinutes(-3));
        var second = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "2", false, false, DateTimeOffset.UtcNow.AddMinutes(-2));
        var third = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "3", false, false, DateTimeOffset.UtcNow.AddMinutes(-1));
        db.InAppNotifications.AddRange(first, second, third);
        await db.SaveChangesAsync();

        var repository = new InAppNotificationRepository(db);
        var page1 = await repository.GetPageAsync(userId, 1, null, null);
        var page2 = await repository.GetPageAsync(userId, 1, third.CreatedAt, third.Id);

        Assert.That(page1, Has.Exactly(2).Items);
        Assert.That(page1[0].Id, Is.EqualTo(third.Id));
        Assert.That(page2, Has.Exactly(2).Items);
        Assert.That(page2[0].Id, Is.EqualTo(second.Id));
    }

    [Test]
    public async Task MarkReadOperationsAndUnreadCount_WorkAsExpected()
    {
        await using var db = CreateDbContext("notification-repo-mark");
        var userId = Id<User>.New();
        var old = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "1", false, false, DateTimeOffset.UtcNow.AddDays(-1));
        var current = CreateNotification(userId, InAppNotificationTypes.InvitationAccepted, "2", false, false, DateTimeOffset.UtcNow);
        var deleted = CreateNotification(userId, InAppNotificationTypes.InvitationRejected, "3", false, true, DateTimeOffset.UtcNow);
        db.InAppNotifications.AddRange(old, current, deleted);
        await db.SaveChangesAsync();

        var repository = new InAppNotificationRepository(db);
        await repository.MarkAsReadAsync(current.Id);
        await repository.MarkAllAsReadAsync(userId, DateTimeOffset.UtcNow.AddHours(-1));
        await db.SaveChangesAsync();

        Assert.That(old.IsRead, Is.True);
        Assert.That(current.IsRead, Is.True);
        Assert.That((await repository.GetUnreadCountAsync(userId)), Is.EqualTo(0));
    }

    [Test]
    public async Task Detach_SetsEntityStateToDetached()
    {
        await using var db = CreateDbContext("notification-repo-detach");
        var entity = CreateNotification(Id<User>.New(), InAppNotificationTypes.InvitationSent, "key", false, false, DateTimeOffset.UtcNow);
        db.InAppNotifications.Add(entity);
        await db.SaveChangesAsync();
        db.Attach(entity);

        var repository = new InAppNotificationRepository(db);
        repository.Detach(entity);

        Assert.That(db.Entry(entity).State, Is.EqualTo(EntityState.Detached));
    }

    [Test]
    public async Task GetRetentionCandidatesCreatedBeforeAsync_WhenRowsAreRetentionEligibleIncludingSoftDeleted_RemovesOldestRowsOlderThanTheCutoff()
    {
        var databaseName = $"notification-repo-retention-{Id<InAppNotificationRepositoryTests>.New():N}";
        await using var db = CreateDbContextForDatabase(databaseName);
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var cutoff = now.AddDays(-90);
        var userId = Id<User>.New();
        var oldest = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "oldest", false, false, cutoff.AddDays(-1));
        oldest.Id = new Id<InAppNotification>(new Guid("00000000-0000-0000-0000-000000000000"));
        var firstExpired = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "first-expired", false, false, cutoff.AddDays(-1));
        firstExpired.Id = new Id<InAppNotification>(new Guid("00000000-0000-0000-0000-000000000001"));
        var secondExpired = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "second-expired", false, false, cutoff.AddDays(-1));
        secondExpired.Id = new Id<InAppNotification>(new Guid("00000000-0000-0000-0000-000000000002"));
        var exactCutoff = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "exact-cutoff", false, false, cutoff);
        var recent = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "recent", false, false, cutoff.AddDays(1));
        var softDeleted = CreateNotification(userId, InAppNotificationTypes.InvitationSent, "soft-deleted", false, true, cutoff.AddDays(-2));
        softDeleted.Id = new Id<InAppNotification>(new Guid("00000000-0000-0000-0000-000000000003"));
        db.InAppNotifications.AddRange(oldest, firstExpired, secondExpired, exactCutoff, recent, softDeleted);
        await db.SaveChangesAsync();

        IInAppNotificationRepository repository = new InAppNotificationRepository(db);

        var candidates = await repository.GetRetentionCandidatesCreatedBeforeAsync(cutoff, 2, []);

        Assert.That(candidates.Select(notification => notification.Id), Is.EqualTo(new[] { softDeleted.Id, oldest.Id }));
        repository.RemoveRange(candidates);

        await using (var uncommittedDatabase = CreateDbContextForDatabase(databaseName))
        {
            var assertedChain41 = (await uncommittedDatabase.InAppNotifications.Select(notification => notification.Id).ToListAsync());
            Assert.That(assertedChain41, Does.Contain(oldest.Id));
            Assert.That(assertedChain41, Does.Contain(firstExpired.Id));
        }

        await db.SaveChangesAsync();

        var remainingIds = await db.InAppNotifications
            .IgnoreQueryFilters()
            .Select(notification => notification.Id)
            .ToListAsync();
        var assertedChain42 = remainingIds;
        Assert.That(assertedChain42, Does.Contain(firstExpired.Id));
        Assert.That(assertedChain42, Does.Contain(secondExpired.Id).And.Contain(exactCutoff.Id).And.Contain(recent.Id));
        var assertedChain43 = remainingIds;
        Assert.That(assertedChain43, Does.Not.Contain(softDeleted.Id));
        Assert.That(assertedChain43, Does.Not.Contain(oldest.Id));
    }

    private static AppDbContext CreateDbContext(string name)
        => CreateDbContextForDatabase($"{name}-{Id<InAppNotificationRepositoryTests>.New():N}");

    private static AppDbContext CreateDbContextForDatabase(string databaseName)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static InAppNotification CreateNotification(Id<User> userId, InAppNotificationType type, string deliveryKey, bool isRead, bool isDeleted, DateTimeOffset createdAt)
        => new()
        {
            Id = Id<InAppNotification>.New(),
            RecipientId = userId,
            Type = type,
            DeliveryKey = deliveryKey,
            IsRead = isRead,
            IsDeleted = isDeleted,
            Message = "message",
            CreatedAt = createdAt
        };
}
