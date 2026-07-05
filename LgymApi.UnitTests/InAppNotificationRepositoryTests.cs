using FluentAssertions;
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

        (await repository.FindByDeliveryKeyAsync(userId, type, "active-key")).Should().NotBeNull();
        (await repository.FindByDeliveryKeyAsync(userId, type, "deleted-key")).Should().BeNull();
        (await repository.GetByIdAsync(deleted.Id)).Should().BeNull();
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

        page1.Should().HaveCount(2);
        page1[0].Id.Should().Be(third.Id);
        page2.Should().HaveCount(2);
        page2[0].Id.Should().Be(second.Id);
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

        old.IsRead.Should().BeTrue();
        current.IsRead.Should().BeTrue();
        (await repository.GetUnreadCountAsync(userId)).Should().Be(0);
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

        db.Entry(entity).State.Should().Be(EntityState.Detached);
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<InAppNotificationRepositoryTests>.New():N}")
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
