using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.IntegrationTests.Authorization;
using LgymApi.Infrastructure.Data;
using NotificationEntity = global::LgymApi.Domain.Entities.InAppNotification;
using NotificationTypes = global::LgymApi.Domain.Notifications.InAppNotificationTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests.InAppNotifications;

[TestFixture]
public sealed class InAppNotificationApiTests : IntegrationTestBase
{
    [Test]
    public async Task GetNotifications_NewUser_ReturnsEmptyPage()
    {
        var user = await SeedUserAsync(name: "notif-empty", email: "notif-empty@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedNotificationsResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().BeEmpty();
        body.HasNextPage.Should().BeFalse();
        body.NextCursorCreatedAt.Should().BeNull();
        body.NextCursorId.Should().BeNull();
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/{id}/notifications", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/{id}/notifications", "own", "foreign-object-denial-no-mutation")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/{id}/notifications", "own", "anonymous-denial")]
    public async Task GetNotificationsRoute_IsOwnerScopedAndNonDisclosing()
    {
        var owner = await SeedUserAsync(name: "notif-http-owner", email: "notif-http-owner@example.test");
        var otherUser = await SeedUserAsync(name: "notif-http-other", email: "notif-http-other@example.test");
        var notification = await SeedNotificationAsync(owner.Id, "HTTP protected notification");

        SetAuthorizationHeader(owner.Id);
        using var ownerResponse = await Client.GetAsync($"/api/{owner.Id}/notifications");
        ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerBody = await ownerResponse.Content.ReadFromJsonAsync<PagedNotificationsResponse>();
        ownerBody.Should().NotBeNull();
        ownerBody!.Items.Should().ContainSingle(item => item.Id == notification.Id.ToString() && item.Message == notification.Message);

        SetAuthorizationHeader(otherUser.Id);
        using var foreignResponse = await Client.GetAsync($"/api/{owner.Id}/notifications");
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var foreignText = await foreignResponse.Content.ReadAsStringAsync();
        foreignText.Should().NotContain(notification.Id.ToString());
        foreignText.Should().NotContain(notification.Message);

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync($"/api/{owner.Id}/notifications");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        anonymousText.Should().NotContain(notification.Id.ToString());
        anonymousText.Should().NotContain(notification.Message);
    }

    [TestCase(0)]
    [TestCase(51)]
    public async Task GetNotifications_LimitOutOfRange_Returns400(int limit)
    {
        var user = await SeedUserAsync(name: $"notif-limit-{limit}", email: $"notif-limit-{limit}@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/notifications?limit={limit}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetNotifications_InvalidCursorId_PreservesTheInitialPageResponse()
    {
        var user = await SeedUserAsync(name: "notif-invalid-cursor", email: "notif-invalid-cursor@example.com");
        var notification = await SeedNotificationAsync(user.Id, "initial-page-notification");
        SetAuthorizationHeader(user.Id);

        var cursorCreatedAt = Uri.EscapeDataString(notification.CreatedAt.ToString("O"));
        var response = await Client.GetAsync($"/api/{user.Id}/notifications?cursorCreatedAt={cursorCreatedAt}&cursorId=not-a-notification-id");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedNotificationsResponse>();
        body.Should().NotBeNull();
        body!.Items.Select(item => item.Id).Should().Contain(notification.Id.ToString());
    }

    [Test]
    public async Task GetNotifications_WithSeededData_ReturnsPaged()
    {
        var user = await SeedUserAsync(name: "notif-page", email: "notif-page@example.com");
        var sender = await SeedUserAsync(name: "notif-page-sender", email: "notif-page-sender@example.com");

        var oldest = await SeedNotificationAsync(user.Id, "first", createdAt: new DateTimeOffset(2026, 4, 4, 8, 0, 0, TimeSpan.Zero));
        var middle = await SeedNotificationAsync(user.Id, "second", senderUserId: sender.Id, createdAt: new DateTimeOffset(2026, 4, 4, 9, 0, 0, TimeSpan.Zero));
        var newest = await SeedNotificationAsync(user.Id, "third", isRead: true, createdAt: new DateTimeOffset(2026, 4, 4, 10, 0, 0, TimeSpan.Zero));

        SetAuthorizationHeader(user.Id);
        var response = await Client.GetAsync($"/api/{user.Id}/notifications?limit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedNotificationsResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
        body.Items.Select(x => x.Id).Should().ContainInOrder(newest.Id.ToString(), middle.Id.ToString());
        body.Items.Select(x => x.Message).Should().ContainInOrder("third", "second");
        body.Items[0].IsRead.Should().BeTrue();
        body.Items[1].SenderUserId.Should().Be(sender.Id.ToString());
        body.HasNextPage.Should().BeTrue();
        body.NextCursorCreatedAt.Should().Be(middle.CreatedAt);
        body.NextCursorId.Should().Be(middle.Id.ToString());
        body.Items.Select(x => x.Id).Should().NotContain(oldest.Id.ToString());
    }

    [Test]
    public async Task MarkAsRead_NotFound_Returns404()
    {
        var user = await SeedUserAsync(name: "notif-missing", email: "notif-missing@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsync($"/api/{user.Id}/notifications/{Id<NotificationEntity>.New()}/mark-read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task MarkAsRead_OtherUser_Returns403()
    {
        var owner = await SeedUserAsync(name: "notif-owner", email: "notif-owner@example.com");
        var otherUser = await SeedUserAsync(name: "notif-other", email: "notif-other@example.com");
        var notification = await SeedNotificationAsync(owner.Id, "owner-only");

        SetAuthorizationHeader(otherUser.Id);
        var response = await Client.PostAsync($"/api/{otherUser.Id}/notifications/{notification.Id}/mark-read", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Notification forbidden");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.InAppNotifications.SingleAsync(x => x.Id == notification.Id);
        stored.IsRead.Should().BeFalse();
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/{id}/notifications/{notificationId}/mark-read", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/{id}/notifications/{notificationId}/mark-read", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/{id}/notifications/{notificationId}/mark-read", "own", "anonymous-denial")]
    public async Task MarkAsRead_OwnNotification_Returns200()
    {
        var user = await SeedUserAsync(name: "notif-read", email: "notif-read@example.com");
        var otherUser = await SeedUserAsync(name: "notif-read-other", email: "notif-read-other@example.com");
        var notification = await SeedNotificationAsync(user.Id, "mark-me");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsync($"/api/{user.Id}/notifications/{notification.Id}/mark-read", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.InAppNotifications.SingleAsync(x => x.Id == notification.Id);
        stored.IsRead.Should().BeTrue();

        SetAuthorizationHeader(otherUser.Id);
        var foreignResponse = await Client.PostAsync($"/api/{user.Id}/notifications/{notification.Id}/mark-read", null);
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousResponse = await Client.PostAsync($"/api/{user.Id}/notifications/{notification.Id}/mark-read", null);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MarkAsReadReplay_AfterSourceLossDeniesCachedResponseWithoutSecondMutation()
    {
        var trainee = await SeedUserAsync("notif-replay-trainee", "notif-replay-trainee@example.com");
        var trainer = await SeedUserAsync("notif-replay-trainer", "notif-replay-trainer@example.com");
        var now = DateTimeOffset.UtcNow;
        Id<DietPlan> dietId;
        Id<NotificationEntity> notificationId;
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var diet = new DietPlan
            {
                Id = Id<DietPlan>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                Name = "Replay source",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = true
            };
            var notification = CreateSourceNotification(
                trainee.Id,
                trainer.Id,
                InAppNotificationTypes.DietPlanUpdated,
                $"diet-plan:{diet.Id}:{now:O}",
                "private replay payload",
                now);
            dietId = diet.Id;
            notificationId = notification.Id;
            database.DietPlans.Add(diet);
            database.InAppNotifications.Add(notification);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        SetIdempotencyKey("notification-source-loss-replay");
        var first = await Client.PostAsync($"/api/{trainee.Id}/notifications/{notificationId}/mark-read", null);
        DateTimeOffset firstUpdatedAt;
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            firstUpdatedAt = (await database.InAppNotifications.SingleAsync(item => item.Id == notificationId)).UpdatedAt;
            (await database.DietPlans.SingleAsync(item => item.Id == dietId)).IsDeleted = true;
            await database.SaveChangesAsync();
        }

        var replay = await Client.PostAsync($"/api/{trainee.Id}/notifications/{notificationId}/mark-read", null);
        ClearIdempotencyKey();

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await replay.Content.ReadAsStringAsync()).Should().NotContain("private replay payload");
        using var assertionScope = Factory.Services.CreateScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await assertionDatabase.InAppNotifications.SingleAsync(item => item.Id == notificationId);
        stored.IsRead.Should().BeTrue();
        stored.UpdatedAt.Should().Be(firstUpdatedAt);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/{id}/notifications/mark-all-read", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/{id}/notifications/mark-all-read", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/{id}/notifications/mark-all-read", "own", "anonymous-denial")]
    public async Task MarkAllAsRead_Returns200()
    {
        var user = await SeedUserAsync(name: "notif-read-all", email: "notif-read-all@example.com");
        var otherUser = await SeedUserAsync(name: "notif-read-all-other", email: "notif-read-all-other@example.com");
        await SeedNotificationAsync(user.Id, "one");
        await SeedNotificationAsync(user.Id, "two");
        await SeedNotificationAsync(user.Id, "already-read", isRead: true);

        SetAuthorizationHeader(user.Id);
        var response = await Client.PostAsync($"/api/{user.Id}/notifications/mark-all-read", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unreadCount = await db.InAppNotifications.CountAsync(x => x.RecipientId == user.Id && !x.IsRead);
        unreadCount.Should().Be(0);

        SetAuthorizationHeader(otherUser.Id);
        var foreignResponse = await Client.PostAsync($"/api/{user.Id}/notifications/mark-all-read", null);
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousResponse = await Client.PostAsync($"/api/{user.Id}/notifications/mark-all-read", null);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUnreadCount_ReturnsCorrectCount()
    {
        var user = await SeedUserAsync(name: "notif-count", email: "notif-count@example.com");
        var otherUser = await SeedUserAsync(name: "notif-count-other", email: "notif-count-other@example.com");

        await SeedNotificationAsync(user.Id, "unread-1");
        await SeedNotificationAsync(user.Id, "unread-2");
        await SeedNotificationAsync(user.Id, "read", isRead: true);
        await SeedNotificationAsync(user.Id, "deleted", isDeleted: true);
        await SeedNotificationAsync(otherUser.Id, "other-user");

        SetAuthorizationHeader(user.Id);
        var response = await Client.GetAsync($"/api/{user.Id}/notifications/unread-count");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
        body.Should().NotBeNull();
        body!.Count.Should().Be(2);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/{id}/notifications/unread-count", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/{id}/notifications/unread-count", "own", "foreign-object-denial-no-mutation")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/{id}/notifications/unread-count", "own", "anonymous-denial")]
    public async Task GetUnreadCountRoute_IsOwnerScopedAndNonDisclosing()
    {
        var owner = await SeedUserAsync(name: "notif-http-count-owner", email: "notif-http-count-owner@example.test");
        var otherUser = await SeedUserAsync(name: "notif-http-count-other", email: "notif-http-count-other@example.test");
        await SeedNotificationAsync(owner.Id, "HTTP unread one");
        await SeedNotificationAsync(owner.Id, "HTTP unread two");

        SetAuthorizationHeader(owner.Id);
        using var ownerResponse = await Client.GetAsync($"/api/{owner.Id}/notifications/unread-count");
        ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerBody = await ownerResponse.Content.ReadFromJsonAsync<UnreadCountResponse>();
        ownerBody.Should().NotBeNull();
        ownerBody!.Count.Should().Be(2);

        SetAuthorizationHeader(otherUser.Id);
        using var foreignResponse = await Client.GetAsync($"/api/{owner.Id}/notifications/unread-count");
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var foreignText = await foreignResponse.Content.ReadAsStringAsync();
        foreignText.Should().NotContain("HTTP unread one");
        foreignText.Should().NotContain("HTTP unread two");

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync($"/api/{owner.Id}/notifications/unread-count");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        anonymousText.Should().NotContain("HTTP unread one");
        anonymousText.Should().NotContain("HTTP unread two");
    }

    [Test]
    public async Task GetNotificationsAndUnreadCount_FilterInvalidSourcesBeforePaging()
    {
        var recipient = await SeedUserAsync(name: "notif-source-recipient", email: "notif-source-recipient@example.test");
        var trainer = await SeedUserAsync(name: "notif-source-trainer", email: "notif-source-trainer@example.test");
        var deletedTrainer = await SeedUserAsync(name: "notif-source-deleted-trainer", email: "notif-source-deleted-trainer@example.test");
        var otherTrainee = await SeedUserAsync(name: "notif-source-other-trainee", email: "notif-source-other-trainee@example.test");
        var now = DateTimeOffset.UtcNow;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validDiet = new DietPlan
            {
                Id = Id<DietPlan>.New(),
                TrainerId = trainer.Id,
                TraineeId = recipient.Id,
                Name = "Current diet",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = true
            };
            var deletedDiet = new DietPlan
            {
                Id = Id<DietPlan>.New(),
                TrainerId = trainer.Id,
                TraineeId = recipient.Id,
                Name = "Deleted diet",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = true,
                IsDeleted = true
            };
            var hiddenNote = new TraineeNote
            {
                Id = Id<TraineeNote>.New(),
                TrainerId = trainer.Id,
                TraineeId = recipient.Id,
                Title = "Hidden note",
                Content = "private",
                VisibleToTrainee = false,
                LastUpdatedByUserId = trainer.Id,
                LastUpdatedAt = now
            };
            var cancelledReport = new ReportRequest
            {
                Id = Id<ReportRequest>.New(),
                TrainerId = trainer.Id,
                TraineeId = recipient.Id,
                TemplateId = Id<ReportTemplate>.New(),
                Status = ReportRequestStatus.Cancelled
            };
            var submittedReminder = CreateReportRequest(trainer.Id, recipient.Id, ReportRequestStatus.Submitted);
            var expiredReminder = CreateReportRequest(trainer.Id, recipient.Id, ReportRequestStatus.Expired);
            var pendingSubmissionRequest = CreateReportRequest(trainer.Id, recipient.Id, ReportRequestStatus.Pending);
            var pendingSubmission = new ReportSubmission
            {
                Id = Id<ReportSubmission>.New(),
                ReportRequestId = pendingSubmissionRequest.Id,
                TraineeId = recipient.Id,
                TrainerFeedbackAddedAt = now
            };
            var mismatchedSubmissionRequest = CreateReportRequest(trainer.Id, recipient.Id, ReportRequestStatus.Submitted);
            var mismatchedSubmission = new ReportSubmission
            {
                Id = Id<ReportSubmission>.New(),
                ReportRequestId = mismatchedSubmissionRequest.Id,
                TraineeId = otherTrainee.Id,
                TrainerFeedbackAddedAt = now
            };
            var deletedSubjectDiet = new DietPlan
            {
                Id = Id<DietPlan>.New(),
                TrainerId = deletedTrainer.Id,
                TraineeId = recipient.Id,
                Name = "Deleted trainer diet",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = true
            };

            db.Users.Single(user => user.Id == deletedTrainer.Id).IsDeleted = true;
            db.DietPlans.AddRange(validDiet, deletedDiet, deletedSubjectDiet);
            db.TraineeNotes.Add(hiddenNote);
            db.ReportRequests.AddRange(
                cancelledReport,
                submittedReminder,
                expiredReminder,
                pendingSubmissionRequest,
                mismatchedSubmissionRequest);
            db.ReportSubmissions.AddRange(pendingSubmission, mismatchedSubmission);
            db.InAppNotifications.AddRange(
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.DietPlanUpdated, $"diet-plan:{validDiet.Id}:{now:O}", "valid source", now.AddMinutes(-5)),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.DietPlanUpdated, $"diet-plan:{deletedDiet.Id}:{now:O}", "deleted source", now.AddMinutes(-4)),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.TraineeNoteUpdated, $"trainee-note:{hiddenNote.Id}:{now:O}", "hidden source", now.AddMinutes(-3)),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.ReportRequestReceived, $"report-request:{cancelledReport.Id}:created", "cancelled source", now.AddMinutes(-2)),
                CreateSourceNotification(recipient.Id, deletedTrainer.Id, InAppNotificationTypes.DietPlanUpdated, $"diet-plan:{deletedSubjectDiet.Id}:{now:O}", "deleted subject", now.AddMinutes(-1)),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.ReportRequestReceived, $"report-request:{submittedReminder.Id}:created", "submitted reminder", now),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.ReportRequestReceived, $"report-request:{expiredReminder.Id}:created", "expired reminder", now.AddSeconds(1)),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.ReportFeedbackReceived, $"report-feedback:{pendingSubmission.Id}:{now:O}", "pending submission", now.AddSeconds(2)),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.ReportFeedbackReceived, $"report-feedback:{mismatchedSubmission.Id}:{now:O}", "mismatched submission trainee", now.AddSeconds(3)),
                CreateSourceNotification(recipient.Id, trainer.Id, InAppNotificationTypes.DietPlanUpdated, $"diet-plan:{validDiet.Id}:arbitrary", "malformed version", now.AddSeconds(4)));
            db.InAppNotifications.AddRange(Enumerable.Range(0, 51).Select(index =>
                CreateSourceNotification(
                    recipient.Id,
                    trainer.Id,
                    InAppNotificationTypes.DietPlanUpdated,
                    $"diet-plan:{validDiet.Id}:invalid-{index}",
                    $"stale recent {index}",
                    now.AddSeconds(5 + index))));
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(recipient.Id);
        var pageResponse = await Client.GetAsync($"/api/{recipient.Id}/notifications?limit=1");
        var countResponse = await Client.GetAsync($"/api/{recipient.Id}/notifications/unread-count");

        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await pageResponse.Content.ReadFromJsonAsync<PagedNotificationsResponse>();
        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle().Which.Message.Should().Be("valid source");
        page.HasNextPage.Should().BeFalse();

        countResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await countResponse.Content.ReadFromJsonAsync<UnreadCountResponse>();
        count.Should().NotBeNull();
        count!.Count.Should().Be(1);
    }

    [Test]
    public async Task GetNotifications_AllCurrentSourceTypesRemainVisibleWithoutActiveRelationship()
    {
        var trainee = await SeedUserAsync(name: "notif-all-trainee", email: "notif-all-trainee@example.test");
        var trainer = await SeedUserAsync(name: "notif-all-trainer", email: "notif-all-trainer@example.test");
        var now = DateTimeOffset.UtcNow;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sentInvitation = CreateInvitation(trainer.Id, trainee.Id, TrainerInvitationStatus.Pending);
            var acceptedInvitation = CreateInvitation(trainer.Id, trainee.Id, TrainerInvitationStatus.Accepted);
            var rejectedInvitation = CreateInvitation(trainer.Id, trainee.Id, TrainerInvitationStatus.Rejected);
            var request = CreateReportRequest(trainer.Id, trainee.Id, ReportRequestStatus.Pending);
            var submittedRequest = CreateReportRequest(trainer.Id, trainee.Id, ReportRequestStatus.Submitted);
            var submission = new ReportSubmission
            {
                Id = Id<ReportSubmission>.New(),
                ReportRequestId = submittedRequest.Id,
                TraineeId = trainee.Id,
                TrainerFeedbackAddedAt = now
            };
            var diet = new DietPlan
            {
                Id = Id<DietPlan>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                Name = "Diet",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = true
            };
            var note = new TraineeNote
            {
                Id = Id<TraineeNote>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                Content = "Visible note",
                VisibleToTrainee = true,
                LastUpdatedByUserId = trainer.Id,
                LastUpdatedAt = now
            };

            db.TrainerInvitations.AddRange(sentInvitation, acceptedInvitation, rejectedInvitation);
            db.ReportRequests.AddRange(request, submittedRequest);
            db.ReportSubmissions.Add(submission);
            db.DietPlans.Add(diet);
            db.TraineeNotes.Add(note);
            db.InAppNotifications.AddRange(
                CreateSourceNotification(trainee.Id, trainer.Id, InAppNotificationTypes.InvitationSent, $"trainer-invitation:{sentInvitation.Id}:sent", "sent", now.AddMinutes(-9)),
                CreateSourceNotification(trainer.Id, trainee.Id, InAppNotificationTypes.InvitationAccepted, $"trainer-invitation:{acceptedInvitation.Id}:accepted", "accepted", now.AddMinutes(-8)),
                CreateSourceNotification(trainer.Id, trainee.Id, InAppNotificationTypes.InvitationRejected, $"trainer-invitation:{rejectedInvitation.Id}:rejected", "rejected", now.AddMinutes(-7)),
                CreateSourceNotification(trainee.Id, trainer.Id, InAppNotificationTypes.ReportRequestReceived, $"report-request:{request.Id}:created", "request", now.AddMinutes(-6)),
                CreateSourceNotification(trainer.Id, trainee.Id, InAppNotificationTypes.ReportSubmissionReceived, $"report-submission:{submission.Id}", "submission", now.AddMinutes(-5)),
                CreateSourceNotification(trainee.Id, trainer.Id, InAppNotificationTypes.ReportFeedbackReceived, $"report-feedback:{submission.Id}:{now:O}", "feedback", now.AddMinutes(-4)),
                CreateSourceNotification(trainee.Id, trainer.Id, InAppNotificationTypes.DietPlanUpdated, $"diet-plan:{diet.Id}:{now:O}", "diet", now.AddMinutes(-3)),
                CreateSourceNotification(trainee.Id, trainer.Id, InAppNotificationTypes.TraineeNoteUpdated, $"trainee-note:{note.Id}:{now:O}", "note", now.AddMinutes(-2)),
                CreateSourceNotification(trainer.Id, trainee.Id, InAppNotificationTypes.TrainerRelationshipEnded, $"trainer-relationship-ended:{trainer.Id}:{trainee.Id}", "ended", now.AddMinutes(-1)));
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        var traineeResponse = await Client.GetAsync($"/api/{trainee.Id}/notifications?limit=20");
        var traineePage = await traineeResponse.Content.ReadFromJsonAsync<PagedNotificationsResponse>();
        traineeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        traineePage!.Items.Select(item => item.Message).Should().BeEquivalentTo("sent", "request", "feedback", "diet", "note");

        SetAuthorizationHeader(trainer.Id);
        var trainerResponse = await Client.GetAsync($"/api/{trainer.Id}/notifications?limit=20");
        var trainerPage = await trainerResponse.Content.ReadFromJsonAsync<PagedNotificationsResponse>();
        trainerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        trainerPage!.Items.Select(item => item.Message).Should().BeEquivalentTo("accepted", "rejected", "submission", "ended");
    }

    private async Task<NotificationEntity> SeedNotificationAsync(
        Id<User> recipientId,
        string message,
        Id<User>? senderUserId = null,
        bool isRead = false,
        bool isDeleted = false,
        DateTimeOffset? createdAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        var notification = new NotificationEntity
        {
            Id = Id<NotificationEntity>.New(),
            RecipientId = recipientId,
            SenderUserId = senderUserId,
            IsSystemNotification = true,
            Message = message,
            RedirectUrl = "/app/notifications",
            IsRead = isRead,
            Type = NotificationTypes.InvitationSent,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            IsDeleted = isDeleted
        };

        db.InAppNotifications.Add(notification);
        await db.SaveChangesAsync();
        return notification;
    }

    private static NotificationEntity CreateSourceNotification(
        Id<User> recipientId,
        Id<User> senderId,
        InAppNotificationType type,
        string deliveryKey,
        string message,
        DateTimeOffset createdAt)
        => new()
        {
            Id = Id<NotificationEntity>.New(),
            RecipientId = recipientId,
            SenderUserId = senderId,
            DeliveryKey = deliveryKey,
            Message = message,
            RedirectUrl = "/source",
            Type = type,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private static TrainerInvitation CreateInvitation(
        Id<User> trainerId,
        Id<User> traineeId,
        TrainerInvitationStatus status)
        => new()
        {
            Id = Id<TrainerInvitation>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            InviteeEmail = $"{status.ToString().ToLowerInvariant()}@example.test",
            Code = Id<TrainerInvitation>.New().ToString(),
            Status = status,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        };

    private static ReportRequest CreateReportRequest(
        Id<User> trainerId,
        Id<User> traineeId,
        ReportRequestStatus status)
        => new()
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = Id<ReportTemplate>.New(),
            Status = status
        };

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class PagedNotificationsResponse
    {
        [JsonPropertyName("items")]
        public List<InAppNotificationResponse> Items { get; set; } = [];

        [JsonPropertyName("hasNextPage")]
        public bool HasNextPage { get; set; }

        [JsonPropertyName("nextCursorCreatedAt")]
        public DateTimeOffset? NextCursorCreatedAt { get; set; }

        [JsonPropertyName("nextCursorId")]
        public string? NextCursorId { get; set; }
    }

    private sealed class InAppNotificationResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("senderUserId")]
        public string? SenderUserId { get; set; }

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }
    }

    private sealed class UnreadCountResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
