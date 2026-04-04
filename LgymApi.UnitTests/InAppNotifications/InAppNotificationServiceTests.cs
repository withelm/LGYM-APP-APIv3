using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Notifications.Application;
using LgymApi.Notifications.Application.Errors;
using LgymApi.Notifications.Application.Models;
using LgymApi.Notifications.Domain;
using NUnit.Framework;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class InAppNotificationServiceTests
{
    [Test]
    public async Task CreateAsync_ValidInput_ReturnsSuccess()
    {
        var repo = new FakeInAppNotificationRepository();
        var service = CreateService(repository: repo);
        var userId = Id<User>.New();
        var input = new CreateInAppNotificationInput(
            userId, null, true, "Test message", null, InAppNotificationTypes.InvitationSent);

        var result = await service.CreateAsync(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Message, Is.EqualTo("Test message"));
        Assert.That(result.Value.Type, Is.EqualTo(InAppNotificationTypes.InvitationSent));
    }

    [Test]
    public async Task CreateAsync_ValidInput_PersistsNotification()
    {
        var repo = new FakeInAppNotificationRepository();
        var service = CreateService(repository: repo);
        var userId = Id<User>.New();
        var input = new CreateInAppNotificationInput(
            userId, null, true, "Persisted", null, InAppNotificationTypes.InvitationSent);

        await service.CreateAsync(input);

        Assert.That(repo.Added, Has.Count.EqualTo(1));
        Assert.That(repo.Added[0].Message, Is.EqualTo("Persisted"));
        Assert.That(repo.Added[0].RecipientId, Is.EqualTo(userId));
        Assert.That(repo.Added[0].IsRead, Is.False);
    }

    [Test]
    public async Task CreateAsync_ValidInput_PushesNotification()
    {
        var push = new FakePushPublisher();
        var service = CreateService(pushPublisher: push);
        var input = new CreateInAppNotificationInput(
            Id<User>.New(), null, true, "Push me", null, InAppNotificationTypes.InvitationSent);

        await service.CreateAsync(input);

        Assert.That(push.PushCalls, Is.EqualTo(1));
        Assert.That(push.LastPushed!.Message, Is.EqualTo("Push me"));
    }

    [Test]
    public async Task CreateAsync_PushThrows_ReturnsSuccess()
    {
        var push = new FakePushPublisher { ThrowOnPush = true };
        var service = CreateService(pushPublisher: push);
        var input = new CreateInAppNotificationInput(
            Id<User>.New(), null, true, "Still ok", null, InAppNotificationTypes.InvitationSent);

        var result = await service.CreateAsync(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Message, Is.EqualTo("Still ok"));
    }

    [Test]
    public async Task GetForUserAsync_NoNotifications_ReturnsEmptyPage()
    {
        var service = CreateService();
        var userId = Id<User>.New();
        var query = new CursorPaginationQuery();

        var result = await service.GetForUserAsync(userId, query);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Items, Is.Empty);
        Assert.That(result.Value.HasNextPage, Is.False);
    }

    [Test]
    public async Task GetForUserAsync_WithNotifications_ReturnsPaged()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        AddNotification(repo, userId, "First");
        AddNotification(repo, userId, "Second");

        var service = CreateService(repository: repo);
        var query = new CursorPaginationQuery(Limit: 20);

        var result = await service.GetForUserAsync(userId, query);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Items, Has.Count.EqualTo(2));
        Assert.That(result.Value.HasNextPage, Is.False);
    }

    [Test]
    public async Task GetForUserAsync_WithCursor_ReturnsNextPage()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        var now = DateTimeOffset.UtcNow;

        // Add 3 notifications with decreasing timestamps to simulate ordered results
        AddNotification(repo, userId, "N1", now.AddMinutes(-3));
        AddNotification(repo, userId, "N2", now.AddMinutes(-2));
        AddNotification(repo, userId, "N3", now.AddMinutes(-1));

        var service = CreateService(repository: repo);

        // First page with limit 2
        var query = new CursorPaginationQuery(Limit: 2);
        var result = await service.GetForUserAsync(userId, query);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Items, Has.Count.EqualTo(2));
        Assert.That(result.Value.HasNextPage, Is.True);
        Assert.That(result.Value.NextCursorCreatedAt, Is.Not.Null);
        Assert.That(result.Value.NextCursorId, Is.Not.Null);
    }

    [Test]
    public async Task MarkAsReadAsync_OwnNotification_ReturnsSuccess()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        var notification = AddNotification(repo, userId, "Read me");

        var service = CreateService(repository: repo);

        var result = await service.MarkAsReadAsync(notification.Id, userId);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task MarkAsReadAsync_OtherUsersNotification_ReturnsForbidden()
    {
        var repo = new FakeInAppNotificationRepository();
        var ownerId = Id<User>.New();
        var otherUserId = Id<User>.New();
        var notification = AddNotification(repo, ownerId, "Not yours");

        var service = CreateService(repository: repo);

        var result = await service.MarkAsReadAsync(notification.Id, otherUserId);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InAppNotificationForbiddenError>());
    }

    [Test]
    public async Task MarkAsReadAsync_NonExistentNotification_ReturnsNotFound()
    {
        var service = CreateService();
        var nonExistentId = Id<InAppNotification>.New();

        var result = await service.MarkAsReadAsync(nonExistentId, Id<User>.New());

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InAppNotificationNotFoundError>());
    }

    [Test]
    public async Task MarkAsReadAsync_AlreadyRead_ReturnsSuccessIdempotent()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        var notification = AddNotification(repo, userId, "Already read");
        notification.IsRead = true;

        var service = CreateService(repository: repo);

        var result = await service.MarkAsReadAsync(notification.Id, userId);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task MarkAllAsReadAsync_MarksAllUnread()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        AddNotification(repo, userId, "Unread 1");
        AddNotification(repo, userId, "Unread 2");

        var service = CreateService(repository: repo);

        var result = await service.MarkAllAsReadAsync(userId, before: null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(repo.MarkAllAsReadCalls, Is.EqualTo(1));
        Assert.That(repo.LastMarkAllAsReadUserId, Is.EqualTo(userId));
        Assert.That(repo.LastMarkAllAsReadBefore, Is.Null);
    }

    [Test]
    public async Task MarkAllAsReadAsync_WithBefore_MarksBefore()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        var before = DateTimeOffset.UtcNow;

        var service = CreateService(repository: repo);

        var result = await service.MarkAllAsReadAsync(userId, before);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(repo.MarkAllAsReadCalls, Is.EqualTo(1));
        Assert.That(repo.LastMarkAllAsReadBefore, Is.EqualTo(before));
    }

    [Test]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        AddNotification(repo, userId, "Unread 1");
        AddNotification(repo, userId, "Unread 2");
        var readNotification = AddNotification(repo, userId, "Read");
        readNotification.IsRead = true;

        var service = CreateService(repository: repo);

        var result = await service.GetUnreadCountAsync(userId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(2));
    }

    #region Helpers

    private static InAppNotificationService CreateService(
        FakeInAppNotificationRepository? repository = null,
        FakeUnitOfWork? unitOfWork = null,
        FakePushPublisher? pushPublisher = null)
    {
        var deps = new TestDependencies(
            repository ?? new FakeInAppNotificationRepository(),
            unitOfWork ?? new FakeUnitOfWork(),
            pushPublisher ?? new FakePushPublisher());
        return new InAppNotificationService(deps);
    }

    private static InAppNotification AddNotification(
        FakeInAppNotificationRepository repo,
        Id<User> userId,
        string message,
        DateTimeOffset? createdAt = null)
    {
        var notification = new InAppNotification
        {
            Id = Id<InAppNotification>.New(),
            RecipientId = userId,
            SenderUserId = null,
            IsSystemNotification = true,
            Message = message,
            RedirectUrl = null,
            IsRead = false,
            Type = InAppNotificationTypes.InvitationSent,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };
        repo.Added.Add(notification);
        return notification;
    }

    #endregion

    #region Fakes

    private sealed class TestDependencies : IInAppNotificationServiceDependencies
    {
        public IInAppNotificationRepository InAppNotificationRepository { get; }
        public IUnitOfWork UnitOfWork { get; }
        public IInAppNotificationPushPublisher PushPublisher { get; }

        public TestDependencies(
            IInAppNotificationRepository repository,
            IUnitOfWork unitOfWork,
            IInAppNotificationPushPublisher pushPublisher)
        {
            InAppNotificationRepository = repository;
            UnitOfWork = unitOfWork;
            PushPublisher = pushPublisher;
        }
    }

    internal sealed class FakeInAppNotificationRepository : IInAppNotificationRepository
    {
        public List<InAppNotification> Added { get; } = new();
        public int MarkAsReadCalls { get; private set; }
        public int MarkAllAsReadCalls { get; private set; }
        public Id<User> LastMarkAllAsReadUserId { get; private set; }
        public DateTimeOffset? LastMarkAllAsReadBefore { get; private set; }

        public Task AddAsync(InAppNotification notification, CancellationToken cancellationToken = default)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task<InAppNotification?> GetByIdAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default)
        {
            var found = Added.FirstOrDefault(n => n.Id == id);
            return Task.FromResult<InAppNotification?>(found);
        }

        public Task<IReadOnlyList<InAppNotification>> GetPageAsync(
            Id<User> userId,
            int limit,
            DateTimeOffset? cursorCreatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken = default)
        {
            var query = Added.Where(n => n.RecipientId == userId);

            if (cursorCreatedAt.HasValue)
            {
                query = query.Where(n =>
                    n.CreatedAt < cursorCreatedAt.Value ||
                    (n.CreatedAt == cursorCreatedAt.Value && n.Id.GetValue().CompareTo(cursorId!.Value) < 0));
            }

            var result = query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id.GetValue())
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<InAppNotification>>(result);
        }

        public Task MarkAsReadAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default)
        {
            MarkAsReadCalls++;
            var notification = Added.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                notification.IsRead = true;
            }
            return Task.CompletedTask;
        }

        public Task MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken cancellationToken = default)
        {
            MarkAllAsReadCalls++;
            LastMarkAllAsReadUserId = userId;
            LastMarkAllAsReadBefore = before;

            var toMark = Added.Where(n => n.RecipientId == userId && !n.IsRead);
            if (before.HasValue)
            {
                toMark = toMark.Where(n => n.CreatedAt < before.Value);
            }
            foreach (var n in toMark.ToList())
            {
                n.IsRead = true;
            }
            return Task.CompletedTask;
        }

        public Task<int> GetUnreadCountAsync(Id<User> userId, CancellationToken cancellationToken = default)
        {
            var count = Added.Count(n => n.RecipientId == userId && !n.IsRead);
            return Task.FromResult(count);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
        }
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePushPublisher : IInAppNotificationPushPublisher
    {
        public int PushCalls { get; private set; }
        public InAppNotificationResult? LastPushed { get; private set; }
        public bool ThrowOnPush { get; set; }

        public Task PushAsync(InAppNotificationResult notification, CancellationToken ct = default)
        {
            PushCalls++;
            LastPushed = notification;
            if (ThrowOnPush)
            {
                throw new InvalidOperationException("Push failed");
            }
            return Task.CompletedTask;
        }
    }

    #endregion
}
