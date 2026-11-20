using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.IntegrationTests.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
public sealed class RecurringReportRequestNowIntegrationTests : PostgreSqlRecurringReportHttpTestBase
{
    private const string CanonicalCommandId =
        "LgymApi.BackgroundWorker.Common.Commands.ReportRequestCreatedInAppNotificationCommand";

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/delete", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/delete", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/delete", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/delete", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/delete", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/pause", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/pause", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/pause", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/pause", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/pause", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/request-now", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/request-now", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/request-now", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/request-now", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/request-now", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/resume", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/resume", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/resume", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/resume", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/resume", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/update", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/update", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/update", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/update", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/update", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainee/report-submissions/{submissionId}/mark-feedback-read", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/report-submissions/{submissionId}/mark-feedback-read", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainee/report-submissions/{submissionId}/mark-feedback-read", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-submissions/{submissionId}/feedback", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-submissions/{submissionId}/feedback", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-submissions/{submissionId}/feedback", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-submissions/{submissionId}/feedback", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/report-submissions/{submissionId}/feedback", "trainer-shared", "anonymous-denial")]
    public async Task RequestNow_EmptyBody_ProvesPendingVisibilityNotificationAndFeedbackReadScheduling()
    {
        var trainer = await SeedTrainerAsync("request-now-trainer", "request-now-trainer@example.com");
        var otherTrainer = await SeedTrainerAsync("request-now-other-trainer", "request-now-other-trainer@example.com");
        var trainee = await SeedUserAsync("request-now-trainee", "request-now-trainee@example.com");
        var otherTrainee = await SeedUserAsync("request-now-other-trainee", "request-now-other-trainee@example.com");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        SetAuthorizationHeader(trainer.Id);

        var templateResponse = await PostAsJsonWithApiOptionsAsync("/api/trainer/report-templates", new
        {
            name = "Request-now check-in",
            fields = new[]
            {
                new { key = "note", label = "Note", type = "Text", isRequired = true, order = 0 }
            }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await ReadAsync<ReportTemplateDto>(templateResponse);

        var assignmentResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments",
            new
            {
                templateId = template.Id,
                intervalValue = 2,
                intervalUnit = RecurringReportIntervalUnit.Day,
                startsAt = DateTimeOffset.UtcNow.AddDays(-1),
                endsAt = DateTimeOffset.UtcNow.AddDays(30),
                note = "Manual cycle"
            });
        Assert.That(assignmentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var assignment = await ReadAsync<RecurringReportAssignmentDto>(assignmentResponse);

        SetIdempotencyKey("request-now-happy-empty-body");
        var requestNowResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/request-now",
            content: null);
        ClearIdempotencyKey();

        Assert.That(requestNowResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var requestNowJson = await requestNowResponse.Content.ReadAsStringAsync();
        using (var document = JsonDocument.Parse(requestNowJson))
        {
            var root = document.RootElement;
            Assert.That(root.TryGetProperty("_id", out _), Is.True);
            Assert.That(root.TryGetProperty("id", out _), Is.False);
            Assert.That(root.TryGetProperty("envelopeId", out _), Is.False);
            Assert.That(root.TryGetProperty("currentReportRequestId", out _), Is.True);
            Assert.That(root.TryGetProperty("currentReportRequest", out var currentRequestJson), Is.True);
            Assert.That(currentRequestJson.GetProperty("status").GetString(), Is.EqualTo(nameof(ReportRequestStatus.Pending)));
            Assert.That(currentRequestJson.TryGetProperty("_id", out _), Is.True);
            Assert.That(currentRequestJson.TryGetProperty("id", out _), Is.False);
        }

        var requestedAssignment = JsonSerializer.Deserialize<RecurringReportAssignmentDto>(requestNowJson, JsonOptions)!;
        Assert.That(requestedAssignment.Id, Is.EqualTo(assignment.Id));
        Assert.That(requestedAssignment.CurrentReportRequest, Is.Not.Null);
        Assert.That(requestedAssignment.CurrentReportRequestId, Is.EqualTo(requestedAssignment.CurrentReportRequest!.Id));
        Assert.That(requestedAssignment.CurrentReportRequest.Status, Is.EqualTo(ReportRequestStatus.Pending));
        Assert.That(requestedAssignment.NextEligibleAt, Is.Null);
        var requestId = ParseId<ReportRequest>(requestedAssignment.CurrentReportRequest.Id);
        var assignmentId = ParseId<RecurringReportAssignment>(assignment.Id);

        await AssertStagedCanonicalCommandAndNoScheduleAsync(trainer.Id, trainee.Id, requestId, assignmentId);

        SetAuthorizationHeader(trainee.Id);
        var pendingResponse = await Client.GetAsync("/api/trainee/report-requests");
        Assert.That(pendingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pending = await ReadAsync<List<ReportRequestDto>>(pendingResponse);
        Assert.That((pending).Where(item =>
            item.Id == requestId.ToString() && item.Status == ReportRequestStatus.Pending), Has.Exactly(1).Items);

        await ProcessPendingCommandsAsync();
        await AssertCanonicalInAppDeliveryAsync(trainer.Id, trainee.Id, requestId);

        var submitResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainee/report-requests/{requestId}/submit",
            new { answers = new { note = "Ready for feedback" } });
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var submission = await ReadAsync<ReportSubmissionDto>(submitResponse);
        await AssertNextEligibleAtIsNullAsync(assignmentId);

        SetAuthorizationHeader(trainer.Id);
        var feedbackResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/report-submissions/{submission.Id}/feedback",
            new
            {
                trainerOverallComment = "Good progress",
                trainerFieldComments = new Dictionary<string, string?> { ["note"] = "Keep going" }
            });
        Assert.That(feedbackResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await AssertNextEligibleAtIsNullAsync(assignmentId);

        var feedbackRequest = new
        {
            trainerOverallComment = "Blocked feedback",
            trainerFieldComments = new Dictionary<string, string?> { ["note"] = "Blocked" }
        };
        SetAuthorizationHeader(otherTrainer.Id);
        var foreignFeedbackResponse = await PostAsJsonWithApiOptionsAsync($"/api/trainer/trainees/{trainee.Id}/report-submissions/{submission.Id}/feedback", feedbackRequest);
        Assert.That(foreignFeedbackResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        SetAuthorizationHeader(otherTrainee.Id);
        var ordinaryFeedbackResponse = await PostAsJsonWithApiOptionsAsync($"/api/trainer/trainees/{trainee.Id}/report-submissions/{submission.Id}/feedback", feedbackRequest);
        Assert.That(ordinaryFeedbackResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousFeedbackResponse = await PostAsJsonWithApiOptionsAsync($"/api/trainer/trainees/{trainee.Id}/report-submissions/{submission.Id}/feedback", feedbackRequest);
        Assert.That(anonymousFeedbackResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(otherTrainee.Id);
        using var foreignFeedbackReadResponse = await Client.PostAsync(
            $"/api/trainee/report-submissions/{submission.Id}/mark-feedback-read",
            content: null);
        Assert.That(foreignFeedbackReadResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        Client.DefaultRequestHeaders.Authorization = null;
        using var anonymousFeedbackReadResponse = await Client.PostAsync(
            $"/api/trainee/report-submissions/{submission.Id}/mark-feedback-read",
            content: null);
        Assert.That(anonymousFeedbackReadResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainee.Id);
        var feedbackReadResponse = await Client.PostAsync(
            $"/api/trainee/report-submissions/{submission.Id}/mark-feedback-read",
            content: null);
        Assert.That(feedbackReadResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var feedbackRead = await ReadAsync<ReportSubmissionDto>(feedbackReadResponse);
        Assert.That(feedbackRead.TrainerFeedbackReadAt, Is.Not.Null);
        Assert.That(feedbackRead.Request.Status, Is.EqualTo(ReportRequestStatus.Submitted));

        using var verificationScope = Factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedSubmission = await verificationDatabase.ReportSubmissions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == ParseId<ReportSubmission>(submission.Id));
        var storedAssignment = await verificationDatabase.RecurringReportAssignments
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == assignmentId);
        Assert.That(storedSubmission.TrainerFeedbackAddedAt, Is.Not.Null);
        Assert.That(storedSubmission.TrainerFeedbackReadAt, Is.Not.Null);
        Assert.That((await verificationDatabase.ReportRequests.AsNoTracking().SingleAsync(candidate => candidate.Id == requestId)).Status, Is.EqualTo(ReportRequestStatus.Completed));
        Assert.That(storedSubmission.TrainerFeedbackReadAt!.Value, Is.EqualTo(feedbackRead.TrainerFeedbackReadAt!.Value).Within(TimeSpan.FromMicroseconds(1)));
        Assert.That(storedAssignment.NextEligibleAt, Is.EqualTo(storedSubmission.TrainerFeedbackReadAt!.Value.AddDays(2)));
        Assert.That(storedAssignment.NextEligibleAt, Is.GreaterThan(storedAssignment.LastRequestCreatedAt!.Value));

        SetAuthorizationHeader(trainer.Id);
        var unrelatedCreateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{otherTrainee.Id}/recurring-report-assignments",
            new { templateId = template.Id, intervalValue = 2, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked unrelated" });
        Assert.That(unrelatedCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        SetAuthorizationHeader(otherTrainee.Id);
        var ordinaryCreateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments",
            new { templateId = template.Id, intervalValue = 2, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked ordinary" });
        Assert.That(ordinaryCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousCreateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments",
            new { templateId = template.Id, intervalValue = 2, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked anonymous" });
        Assert.That(anonymousCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        var ownerPauseResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/pause",
            content: null);
        Assert.That(ownerPauseResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var ownerResumeResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/resume",
            content: null);
        Assert.That(ownerResumeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var foreignPauseResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{otherTrainee.Id}/recurring-report-assignments/{assignment.Id}/pause",
            content: null);
        Assert.That(foreignPauseResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var foreignResumeResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{otherTrainee.Id}/recurring-report-assignments/{assignment.Id}/resume",
            content: null);
        Assert.That(foreignResumeResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        SetAuthorizationHeader(otherTrainee.Id);
        var ordinaryPauseResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/pause",
            content: null);
        Assert.That(ordinaryPauseResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryResumeResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/resume",
            content: null);
        Assert.That(ordinaryResumeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousPauseResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/pause",
            content: null);
        Assert.That(anonymousPauseResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousResumeResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/resume",
            content: null);
        Assert.That(anonymousResumeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        var foreignRequestNowResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{otherTrainee.Id}/recurring-report-assignments/{assignment.Id}/request-now",
            content: null);
        Assert.That(foreignRequestNowResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        SetAuthorizationHeader(otherTrainee.Id);
        var ordinaryRequestNowResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/request-now",
            content: null);
        Assert.That(ordinaryRequestNowResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousRequestNowResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/request-now",
            content: null);
        Assert.That(anonymousRequestNowResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        var ownerUpdateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/update",
            new { templateId = template.Id, intervalValue = 3, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Updated cycle" });
        Assert.That(ownerUpdateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var foreignUpdateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{otherTrainee.Id}/recurring-report-assignments/{assignment.Id}/update",
            new { templateId = template.Id, intervalValue = 3, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked unrelated update" });
        Assert.That(foreignUpdateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        SetAuthorizationHeader(otherTrainee.Id);
        var ordinaryUpdateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/update",
            new { templateId = template.Id, intervalValue = 3, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked ordinary update" });
        Assert.That(ordinaryUpdateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousUpdateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/update",
            new { templateId = template.Id, intervalValue = 3, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked anonymous update" });
        Assert.That(anonymousUpdateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        var ownerDeleteResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/delete",
            content: null);
        Assert.That(ownerDeleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var foreignDeleteResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{otherTrainee.Id}/recurring-report-assignments/{assignment.Id}/delete",
            content: null);
        Assert.That(foreignDeleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        SetAuthorizationHeader(otherTrainee.Id);
        var ordinaryDeleteResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/delete",
            content: null);
        Assert.That(ordinaryDeleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        Client.DefaultRequestHeaders.Authorization = null;
        var anonymousDeleteResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/delete",
            content: null);
        Assert.That(anonymousDeleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        SetAuthorizationHeader(trainer.Id);
        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks.SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        var formerCreateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments",
            new { templateId = template.Id, intervalValue = 2, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked former" });
        Assert.That(formerCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerFeedbackResponse = await PostAsJsonWithApiOptionsAsync($"/api/trainer/trainees/{trainee.Id}/report-submissions/{submission.Id}/feedback", feedbackRequest);
        Assert.That(formerFeedbackResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerPauseResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/pause",
            content: null);
        Assert.That(formerPauseResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerResumeResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/resume",
            content: null);
        Assert.That(formerResumeResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerUpdateResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/update",
            new { templateId = template.Id, intervalValue = 3, intervalUnit = RecurringReportIntervalUnit.Day, startsAt = DateTimeOffset.UtcNow, note = "Blocked former update" });
        Assert.That(formerUpdateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerRequestNowResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/request-now",
            content: null);
        Assert.That(formerRequestNowResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var formerDeleteResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments/{assignment.Id}/delete",
            content: null);
        Assert.That(formerDeleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private async Task AssertStagedCanonicalCommandAndNoScheduleAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        Id<ReportRequest> requestId,
        Id<RecurringReportAssignment> assignmentId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var envelope = await database.CommandEnvelopes.AsNoTracking().SingleAsync(candidate =>
            candidate.CommandTypeFullName == CanonicalCommandId);
        using var payload = JsonDocument.Parse(envelope.PayloadJson);
        Assert.That(payload.RootElement.GetProperty("requestId").GetString(), Is.EqualTo(requestId.ToString()));
        Assert.That(payload.RootElement.GetProperty("traineeId").GetString(), Is.EqualTo(traineeId.ToString()));
        Assert.That(payload.RootElement.GetProperty("trainerId").GetString(), Is.EqualTo(trainerId.ToString()));
        Assert.That(payload.RootElement.TryGetProperty("envelopeId", out _), Is.False);
        var storedAssignment = await database.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == assignmentId);
        Assert.That(storedAssignment.NextEligibleAt, Is.Null);
    }

    private async Task AssertCanonicalInAppDeliveryAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        Id<ReportRequest> requestId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var envelope = await database.CommandEnvelopes.AsNoTracking().SingleAsync(candidate =>
            candidate.CommandTypeFullName == CanonicalCommandId);
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        var notification = await database.InAppNotifications.AsNoTracking().SingleAsync(candidate =>
            candidate.RecipientId == traineeId && candidate.Type == InAppNotificationTypes.ReportRequestReceived);
        Assert.That(notification.SenderUserId, Is.EqualTo(trainerId));
        Assert.That(notification.DeliveryKey, Is.EqualTo($"report-request:{requestId}:created"));
        Assert.That(notification.RedirectUrl, Is.EqualTo($"/trainer/report-requests/{requestId}"));
        Assert.That(notification.IsRead, Is.False);
    }

    private async Task AssertNextEligibleAtIsNullAsync(Id<RecurringReportAssignment> assignmentId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nextEligibleAt = await database.RecurringReportAssignments.AsNoTracking()
            .Where(candidate => candidate.Id == assignmentId)
            .Select(candidate => candidate.NextEligibleAt)
            .SingleAsync();
        Assert.That(nextEligibleAt, Is.Null);
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name, email);
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
        await database.SaveChangesAsync();
        return trainer;
    }

    private async Task LinkTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        });
        await database.SaveChangesAsync();
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) where T : notnull =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private static Id<TEntity> ParseId<TEntity>(string value)
    {
        Assert.That(Id<TEntity>.TryParse(value, out var id), Is.True);
        return id;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
