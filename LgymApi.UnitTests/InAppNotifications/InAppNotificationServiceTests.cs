using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using InAppNotificationService = global::LgymApi.Application.Notifications.InAppNotificationService;
using IInAppNotificationRepository = global::LgymApi.Application.Notifications.IInAppNotificationRepository;
using IInAppNotificationPushPublisher = global::LgymApi.Application.Notifications.IInAppNotificationPushPublisher;
using INotificationEventBridge = global::LgymApi.Application.Notifications.INotificationEventBridge;
using NotificationDisclosureEligibilityService = global::LgymApi.Application.Notifications.NotificationDisclosureEligibilityService;
using LgymApi.Application.Notifications.Errors;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Notifications.Authorization;
using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Platform.Contracts;
using LgymApi.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class InAppNotificationServiceTests
{
    [Test]
    public void InAppNotificationService_UsesTheFourDirectDeliveryCollaborators()
    {
        var assertedValue184 = (typeof(InAppNotificationService).GetConstructors()).ToArray();
        Assert.That(assertedValue184, Has.Exactly(1).Items);
        var constructor = assertedValue184.Single();

        Assert.That(constructor.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(IInAppNotificationRepository), typeof(IUnitOfWork), typeof(IInAppNotificationPushPublisher), typeof(INotificationEventBridge), typeof(Microsoft.Extensions.Logging.ILogger<InAppNotificationService>), typeof(LgymApi.Authorization.Contracts.IAuthorizationEvaluator<NotificationOwnershipContext>), typeof(NotificationDisclosureEligibilityService) }));
    }

    [Test]
    public async Task CreateAsync_ValidInput_ReturnsSuccess()
    {
        var repo = new FakeInAppNotificationRepository();
        var service = CreateService(repository: repo);
        var userId = Id<User>.New();
        var input = new CreateInAppNotificationInput(
            userId, null, null, true, "Test message", null, InAppNotificationTypes.InvitationSent);

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
            userId, null, null, true, "Persisted", null, InAppNotificationTypes.InvitationSent);

        await service.CreateAsync(input);

        Assert.That(repo.Added, Has.Exactly(1).Items);
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
            Id<User>.New(), null, null, true, "Push me", null, InAppNotificationTypes.InvitationSent);

        await service.CreateAsync(input);

        Assert.That(push.PushCalls, Is.EqualTo(1));
        Assert.That(push.LastPushed!.Message, Is.EqualTo("Push me"));
    }

    [Test]
    public async Task CreateAsync_ValidInput_EnqueuesRemotePushNotification()
    {
        var bridge = new FakeNotificationEventBridge();
        var service = CreateService(notificationEventBridge: bridge);
        var userId = Id<User>.New();
        var input = new CreateInAppNotificationInput(
            userId,
            null,
            null,
            true,
            "Report request received",
            "/trainer/report-requests/request-1",
            InAppNotificationTypes.ReportRequestReceived);

        var result = await service.CreateAsync(input);

        Assert.That(bridge.EnqueueCalls, Is.EqualTo(1));
        Assert.That(bridge.LastInput, Is.Not.Null);
        Assert.That(bridge.LastInput!.UserId, Is.EqualTo(userId));
        Assert.That(bridge.LastInput.SchemaVersion, Is.EqualTo(1));
        Assert.That(bridge.LastInput.Type, Is.EqualTo(InAppNotificationTypes.ReportRequestReceived.Value));
        Assert.That(bridge.LastInput.EventKey, Is.EqualTo(result.Value.Id.ToString()));
        Assert.That(bridge.LastInput.EntityKey, Is.Null);
        Assert.That(bridge.LastInput.InAppNotificationId, Is.EqualTo(result.Value.Id));
        Assert.That(bridge.LastInput.Deeplink, Is.EqualTo("/trainer/report-requests/request-1"));
    }

    [Test]
    public async Task CreateAsync_RemotePushEnqueueThrows_PropagatesForRetry()
    {
        var bridge = new FakeNotificationEventBridge { ThrowOnEnqueue = true };
        var service = CreateService(notificationEventBridge: bridge);
        var input = new CreateInAppNotificationInput(
            Id<User>.New(), null, null, true, "Still ok", null, InAppNotificationTypes.InvitationSent);

        var action = async () => await service.CreateAsync(input);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Remote push enqueue failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(bridge.EnqueueCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateAsync_PushThrows_ReturnsSuccess()
    {
        var push = new FakePushPublisher { ThrowOnPush = true };
        var service = CreateService(pushPublisher: push);
        var input = new CreateInAppNotificationInput(
            Id<User>.New(), null, null, true, "Still ok", null, InAppNotificationTypes.InvitationSent);

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
        Assert.That(result.Value.Items, Has.Exactly(2).Items);
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
        Assert.That(result.Value.Items, Has.Exactly(2).Items);
        Assert.That(result.Value.HasNextPage, Is.True);
        Assert.That(result.Value.NextCursorCreatedAt, Is.Not.Null);
        Assert.That(result.Value.NextCursorId, Is.Not.Null);
    }

    [Test]
    public async Task GetForUserAsync_MoreThanFiftyInvalidRecentSources_DoesNotHideOlderValidNotification()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        var now = DateTimeOffset.UtcNow;
        AddNotification(repo, userId, "older valid", now.AddMinutes(-52));
        for (var index = 0; index < 51; index++)
        {
            var notification = AddNotification(repo, userId, $"stale {index}", now.AddMinutes(-index));
            notification.SenderUserId = Id<User>.New();
            notification.DeliveryKey = "malformed";
            notification.IsSystemNotification = false;
        }

        var result = await CreateService(repository: repo).GetForUserAsync(
            userId,
            new CursorPaginationQuery(Limit: 1));

        var assertedValue185 = (result.Value.Items).ToArray();
        Assert.That(assertedValue185, Has.Exactly(1).Items);
        Assert.That(assertedValue185.Single().Message, Is.EqualTo("older valid"));
        Assert.That(result.Value.HasNextPage, Is.False);
    }

    [Test]
    public async Task GetForUserAsync_ExactlyFiftyResultsAndOneOlderRow_SetsNextPageCursor()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 51; index++)
        {
            AddNotification(repo, userId, $"notification {index}", now.AddMinutes(-index));
        }

        var result = await CreateService(repository: repo).GetForUserAsync(
            userId,
            new CursorPaginationQuery(Limit: 50));

        Assert.That(result.Value.Items, Has.Exactly(50).Items);
        Assert.That(result.Value.HasNextPage, Is.True);
        Assert.That(result.Value.NextCursorCreatedAt, Is.EqualTo(now.AddMinutes(-49)));
        Assert.That(result.Value.NextCursorId, Is.EqualTo(result.Value.Items[^1].Id));
    }

    [Test]
    public async Task MarkAsReadAsync_OwnNotification_ReturnsSuccess()
    {
        var repo = new FakeInAppNotificationRepository();
        var userId = Id<User>.New();
        var notification = AddNotification(repo, userId, "Read me");

        var service = CreateService(repository: repo);

        var result = await service.MarkAsReadAsync(notification.Id, Actor(userId));

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

        var result = await service.MarkAsReadAsync(notification.Id, Actor(otherUserId));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InAppNotificationForbiddenError>());
    }

    [Test]
    public async Task MarkAsReadAsync_NonExistentNotification_ReturnsNotFound()
    {
        var service = CreateService();
        var nonExistentId = Id<InAppNotification>.New();

        var result = await service.MarkAsReadAsync(nonExistentId, Actor(Id<User>.New()));

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

        var result = await service.MarkAsReadAsync(notification.Id, Actor(userId));

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

    [Test]
    public async Task CreateAsync_DuplicateDeliveryKey_ReturnsExistingNotificationWithoutSignalRPushAndEnqueuesRemotePush()
    {
        var repo = new FakeInAppNotificationRepository();
        var unitOfWork = new FakeUnitOfWork
        {
            SaveChangesException = new DbUpdateException("duplicate IX_in_app_notifications_RecipientId_Type_DeliveryKey")
        };
        var push = new FakePushPublisher();
        var bridge = new FakeNotificationEventBridge();
        var userId = Id<User>.New();
        var invitationId = Id<TrainerInvitation>.New();
        var existing = AddNotification(repo, userId, "Already there");
        existing.Type = InAppNotificationTypes.InvitationSent;
        existing.SenderUserId = Id<User>.New();
        existing.IsSystemNotification = false;
        existing.DeliveryKey = $"trainer-invitation:{invitationId}:sent";

        var service = CreateService(repository: repo, unitOfWork: unitOfWork, pushPublisher: push, notificationEventBridge: bridge);
        var input = new CreateInAppNotificationInput(
            userId,
            existing.SenderUserId,
            existing.DeliveryKey,
            false,
            "Already there",
            null,
            InAppNotificationTypes.InvitationSent);

        var result = await service.CreateAsync(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Id, Is.EqualTo(existing.Id));
        Assert.That(push.PushCalls, Is.EqualTo(0));
        Assert.That(bridge.EnqueueCalls, Is.EqualTo(1));
        Assert.That(bridge.LastInput!.InAppNotificationId, Is.EqualTo(existing.Id));
        Assert.That(bridge.LastInput.EventKey, Is.EqualTo(existing.Id.ToString()));
        Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        Assert.That(repo.DetachCalls, Is.EqualTo(1));
    }

    #region Helpers

    private static InAppNotificationService CreateService(
        FakeInAppNotificationRepository? repository = null,
        FakeUnitOfWork? unitOfWork = null,
        FakePushPublisher? pushPublisher = null,
        FakeNotificationEventBridge? notificationEventBridge = null)
    {
        var selectedRepository = repository ?? new FakeInAppNotificationRepository();
        return new InAppNotificationService(
            selectedRepository,
            unitOfWork ?? new FakeUnitOfWork(),
            pushPublisher ?? new FakePushPublisher(),
            notificationEventBridge ?? new FakeNotificationEventBridge(),
            NullLogger<InAppNotificationService>.Instance,
            new AuthorizationPolicyEvaluator<NotificationOwnershipContext>([new NotificationOwnershipPolicy()]),
            new NotificationDisclosureEligibilityService(selectedRepository, new FakeNotificationSourceFactPort()));
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
            DeliveryKey = null,
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

    internal sealed class FakeInAppNotificationRepository : IInAppNotificationRepository
    {
        public List<InAppNotification> Added { get; } = new();
        public int DetachCalls { get; private set; }
        public int MarkAsReadCalls { get; private set; }
        public int MarkAllAsReadCalls { get; private set; }
        public Id<User> LastMarkAllAsReadUserId { get; private set; }
        public DateTimeOffset? LastMarkAllAsReadBefore { get; private set; }

        public Task AddAsync(InAppNotification notification, CancellationToken cancellationToken = default)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task<InAppNotification?> FindByDeliveryKeyAsync(
            Id<User> recipientId,
            InAppNotificationType type,
            string deliveryKey,
            CancellationToken cancellationToken = default)
        {
            var found = Added.FirstOrDefault(n => n.RecipientId == recipientId && n.Type == type && n.DeliveryKey == deliveryKey);
            return Task.FromResult<InAppNotification?>(found);
        }

        public void Detach(InAppNotification notification)
        {
            DetachCalls++;
            Added.Remove(notification);
        }

        public Task<InAppNotification?> GetByIdAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default)
        {
            var found = Added.FirstOrDefault(n => n.Id == id);
            return Task.FromResult<InAppNotification?>(found);
        }

        public Task<bool> IsActiveAsync(Id<InAppNotification> id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Added.Any(n => n.Id == id && !n.IsDeleted));
        }

        public Task<IReadOnlyList<InAppNotification>> GetPageAsync(
            Id<User> userId,
            int limit,
            DateTimeOffset? cursorCreatedAt,
            Id<InAppNotification>? cursorId,
            CancellationToken cancellationToken = default)
        {
            var query = Added.Where(n => n.RecipientId == userId);

            if (cursorCreatedAt.HasValue && cursorId.HasValue)
            {
                query = query.Where(n =>
                    n.CreatedAt < cursorCreatedAt.Value ||
                    (n.CreatedAt == cursorCreatedAt.Value && n.Id.GetValue().CompareTo(cursorId.Value.GetValue()) < 0));
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

        public Task<IReadOnlyList<InAppNotification>> GetUnreadAsync(
            Id<User> userId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InAppNotification> unread = Added
                .Where(notification => notification.RecipientId == userId
                    && !notification.IsRead
                    && !notification.IsDeleted)
                .ToList();
            return Task.FromResult(unread);
        }
    }

    private static AuthorizationActor Actor(Id<User> userId) =>
        new(userId.Rebind<ActorReference>());

    private sealed class FakeNotificationSourceFactPort : INotificationSourceFactPort
    {
        public Task<bool> StageReminderFenceAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyDictionary<Id<InAppNotification>, NotificationSourceFacts>> GetFactsAsync(
            IReadOnlyCollection<NotificationSourceReference> references,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Id<InAppNotification>, NotificationSourceFacts>>(
                references.ToDictionary(
                    reference => reference.NotificationId,
                    reference => new NotificationSourceFacts(
                        true,
                        false,
                        true,
                        true,
                        true,
                        reference.RequiredState,
                        true,
                        true,
                        false)));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Exception? SaveChangesException { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            if (SaveChangesException != null)
            {
                throw SaveChangesException;
            }

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

    private sealed class FakeNotificationEventBridge : INotificationEventBridge
    {
        public int EnqueueCalls { get; private set; }
        public EnqueueNotificationEventInput? LastInput { get; private set; }
        public bool ThrowOnEnqueue { get; set; }

        public Task EnqueueAsync(EnqueueNotificationEventInput input, CancellationToken cancellationToken = default)
        {
            EnqueueCalls++;
            LastInput = input;
            if (ThrowOnEnqueue)
            {
                throw new InvalidOperationException("Remote push enqueue failed");
            }

            return Task.CompletedTask;
        }
    }

    #endregion
}
