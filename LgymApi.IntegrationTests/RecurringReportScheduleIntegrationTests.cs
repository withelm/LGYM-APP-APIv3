using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.IntegrationTests.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
public sealed class RecurringReportScheduleIntegrationTests : PostgreSqlRecurringReportHttpTestBase
{
    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/recurring-report-assignments", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/recurring-report-assignments", "own", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainee/recurring-report-assignments", "own", "no-client-subject")]
    [AuthorizationEvidence("POST", "/api/trainee/recurring-report-assignments/{assignmentId}/next-report-proposals", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/recurring-report-assignments/{assignmentId}/next-report-proposals", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainee/recurring-report-assignments/{assignmentId}/next-report-proposals", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-override", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-override", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-override", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-override", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-override", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/accept", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/accept", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/accept", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/accept", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/accept", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/reject", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/reject", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/reject", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/reject", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/recurring-report-assignments/{id}/next-report-proposals/{proposalId}/reject", "trainer-shared", "anonymous-denial")]
    public async Task ScheduleWorkflow_EnforcesOffsetRevisionProposalAcceptanceAndRequestNowConsumption()
    {
        var scenario = await CreateScenarioAsync("schedule-workflow");
        var overrideAt = DateTimeOffset.UtcNow.AddDays(2).ToOffset(TimeSpan.FromHours(2));

        SetIdempotencyKey("schedule-workflow-invalid-offset");
        using var invalidOffsetResponse = await Client.PostAsync(
            TrainerOverrideUrl(scenario),
            new StringContent(
                $$"""{"scheduledAt":"{{overrideAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture)}}","expectedScheduleRevision":0}""",
                Encoding.UTF8,
                "application/json"));
        ClearIdempotencyKey();
        Assert.That(invalidOffsetResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var overrideResponse = await PostAsJsonWithApiOptionsAsync(
            TrainerOverrideUrl(scenario),
            new { scheduledAt = overrideAt, expectedScheduleRevision = 0 });
        Assert.That(overrideResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var overridden = await ReadAsync<RecurringReportAssignmentDto>(overrideResponse);
        Assert.That(overridden.ScheduleRevision, Is.EqualTo(1));
        Assert.That(overridden.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(overrideAt.ToUniversalTime()).Within(TimeSpan.FromMicroseconds(1)));

        var staleResponse = await PostAsJsonWithApiOptionsAsync(
            TrainerOverrideUrl(scenario),
            new { scheduledAt = overrideAt.AddHours(1), expectedScheduleRevision = 0 });
        Assert.That(staleResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        SetAuthorizationHeader(scenario.Trainee.Id);
        var proposalAt = overrideAt.AddDays(1);
        var proposalResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainee/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals",
            new { scheduledAt = proposalAt, reason = "Travel day", expectedScheduleRevision = 1 });
        Assert.That(proposalResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var proposed = await ReadAsync<RecurringReportAssignmentDto>(proposalResponse);
        Assert.That(proposed.ScheduleRevision, Is.EqualTo(1));
        Assert.That(proposed.PendingScheduleProposal, Is.Not.Null);
        Assert.That(proposed.PendingScheduleProposal!.Reason, Is.EqualTo("Travel day"));
        Assert.That(proposed.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(overrideAt.ToUniversalTime()).Within(TimeSpan.FromMicroseconds(1)));

        SetAuthorizationHeader(scenario.Trainer.Id);
        SetIdempotencyKey("schedule-workflow-accept");
        using var acceptResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals/{proposed.PendingScheduleProposal.Id}/accept",
            content: null);
        ClearIdempotencyKey();
        Assert.That(acceptResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var accepted = await ReadAsync<RecurringReportAssignmentDto>(acceptResponse);
        Assert.That(accepted.ScheduleRevision, Is.EqualTo(2));
        Assert.That(accepted.PendingScheduleProposal, Is.Null);
        Assert.That(accepted.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(proposalAt.ToUniversalTime()).Within(TimeSpan.FromMicroseconds(1)));

        await AssertProposalHistoryVisibleToBothActorsAsync(
            scenario,
            [
                new ProposalAuditExpectation(
                    proposed.PendingScheduleProposal.Id,
                    RecurringReportScheduleChangeStatus.Approved,
                    "Travel day",
                    1)
            ]);

        SetAuthorizationHeader(scenario.Trainee.Id);
        var rejectedProposalAt = proposalAt.AddDays(1);
        var rejectedProposalResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainee/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals",
            new { scheduledAt = rejectedProposalAt, reason = "Keep original", expectedScheduleRevision = 2 });
        Assert.That(rejectedProposalResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pendingRejection = await ReadAsync<RecurringReportAssignmentDto>(rejectedProposalResponse);
        Assert.That(pendingRejection.PendingScheduleProposal, Is.Not.Null);

        SetAuthorizationHeader(scenario.Trainer.Id);
        SetIdempotencyKey("schedule-workflow-reject");
        using var rejectResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals/{pendingRejection.PendingScheduleProposal!.Id}/reject",
            content: null);
        ClearIdempotencyKey();
        Assert.That(rejectResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await AssertProposalHistoryVisibleToBothActorsAsync(
            scenario,
            [
                new ProposalAuditExpectation(
                    pendingRejection.PendingScheduleProposal.Id,
                    RecurringReportScheduleChangeStatus.Rejected,
                    "Keep original",
                    2),
                new ProposalAuditExpectation(
                    proposed.PendingScheduleProposal.Id,
                    RecurringReportScheduleChangeStatus.Approved,
                    "Travel day",
                    1)
            ]);

        SetIdempotencyKey("schedule-workflow-request-now");
        using var requestNowResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/request-now",
            content: null);
        ClearIdempotencyKey();
        Assert.That(requestNowResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var requested = await ReadAsync<RecurringReportAssignmentDto>(requestNowResponse);
        Assert.That(requested.ScheduleRevision, Is.EqualTo(3));
        Assert.That(requested.PendingScheduleProposal, Is.Null);
        Assert.That(requested.EffectiveNextRecurringRequest.Status, Is.EqualTo(NextRecurringReportRequestStatus.Unscheduled));

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var changes = await database.RecurringReportScheduleChanges
            .AsNoTracking()
            .Where(change => change.RecurringReportAssignmentId == ParseId<RecurringReportAssignment>(scenario.Assignment.Id))
            .ToListAsync();
        Assert.That((changes).Where(change =>
            change.Kind == RecurringReportScheduleChangeKind.TraineeProposal
            && change.Status == RecurringReportScheduleChangeStatus.Approved), Has.Exactly(1).Items);
        Assert.That((changes).Where(change =>
            change.Kind == RecurringReportScheduleChangeKind.TrainerOverride
            && change.Status == RecurringReportScheduleChangeStatus.Superseded), Has.Exactly(1).Items);
        Assert.That((changes).Where(change =>
            change.Kind == RecurringReportScheduleChangeKind.TrainerOverride
            && change.Status == RecurringReportScheduleChangeStatus.Consumed), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ScheduleMutation_AfterUnlinkAndRelink_DoesNotCrossTheSourceEpisodeBoundary()
    {
        var scenario = await CreateScenarioAsync("schedule-relink");

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks.SingleAsync(candidate =>
                candidate.TrainerId == scenario.Trainer.Id && candidate.TraineeId == scenario.Trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = scenario.Trainer.Id,
                TraineeId = scenario.Trainee.Id
            });
            await database.SaveChangesAsync();
        }

        var response = await PostAsJsonWithApiOptionsAsync(
            TrainerOverrideUrl(scenario),
            new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2), expectedScheduleRevision = 0 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RequestNow_DeletedTemplate_WithCurrentSource_DeactivatesAndClosesScheduleLifecycle()
    {
        var scenario = await CreateScenarioAsync("deleted-template-current-source");
        var schedule = await CreatePendingScheduleStateAsync(scenario);
        await DeleteTemplateAsync(scenario.Assignment.TemplateId);

        using var response = await Client.PostAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/request-now",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignmentId = ParseId<RecurringReportAssignment>(scenario.Assignment.Id);
        var assignment = await database.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == assignmentId);
        var changes = await database.RecurringReportScheduleChanges.AsNoTracking()
            .Where(change => change.RecurringReportAssignmentId == assignmentId)
            .ToListAsync();
        Assert.That(assignment.IsActive, Is.False);
        Assert.That(assignment.ScheduleRevision, Is.EqualTo(2));
        Assert.That(changes.Single(change => change.Id == schedule.OverrideId).Status, Is.EqualTo(RecurringReportScheduleChangeStatus.Superseded));
        Assert.That(changes.Single(change => change.Id == schedule.ProposalId).Status, Is.EqualTo(RecurringReportScheduleChangeStatus.Invalidated));
    }

    [Test]
    public async Task RequestNow_DeletedTemplate_AfterSameTrainerRelink_DeniesWithoutLifecycleMutation()
    {
        var scenario = await CreateScenarioAsync("deleted-template-relink");
        var schedule = await CreatePendingScheduleStateAsync(scenario);
        await DeleteTemplateAsync(scenario.Assignment.TemplateId);
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks.SingleAsync(candidate =>
                candidate.TrainerId == scenario.Trainer.Id && candidate.TraineeId == scenario.Trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
            var replacementEpisode = new CoachingEpisode
            {
                Id = Id<CoachingEpisode>.New(),
                TrainerId = scenario.Trainer.Id,
                TraineeId = scenario.Trainee.Id,
                StartedAt = DateTimeOffset.UtcNow,
                StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
            };
            database.AddRange(
                replacementEpisode,
                new TrainerTraineeLink
                {
                    Id = Id<TrainerTraineeLink>.New(),
                    TrainerId = scenario.Trainer.Id,
                    TraineeId = scenario.Trainee.Id,
                    EpisodeId = replacementEpisode.Id
                });
            await database.SaveChangesAsync();
        }

        using var response = await Client.PostAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/request-now",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignmentId = ParseId<RecurringReportAssignment>(scenario.Assignment.Id);
        var assignment = await assertionDatabase.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == assignmentId);
        var changes = await assertionDatabase.RecurringReportScheduleChanges.AsNoTracking()
            .Where(change => change.RecurringReportAssignmentId == assignmentId)
            .ToListAsync();
        Assert.That(assignment.IsActive, Is.True);
        Assert.That(assignment.ScheduleRevision, Is.EqualTo(1));
        Assert.That(changes.Single(change => change.Id == schedule.OverrideId).Status, Is.EqualTo(RecurringReportScheduleChangeStatus.Active));
        Assert.That(changes.Single(change => change.Id == schedule.ProposalId).Status, Is.EqualTo(RecurringReportScheduleChangeStatus.Pending));
    }

    private async Task<ScheduleScenario> CreateScenarioAsync(string prefix)
    {
        var trainer = await SeedUserAsync($"{prefix}-trainer", $"{prefix}-trainer@example.com");
        var trainee = await SeedUserAsync($"{prefix}-trainee", $"{prefix}-trainee@example.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await PostAsJsonWithApiOptionsAsync("/api/trainer/report-templates", new
        {
            name = $"{prefix} template",
            fields = new[] { new { key = "note", label = "Note", type = "Text", isRequired = true, order = 0 } }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var template = await ReadAsync<ReportTemplateDto>(templateResponse);
        var assignmentResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainer/trainees/{trainee.Id}/recurring-report-assignments",
            new
            {
                templateId = template.Id,
                intervalValue = 1,
                intervalUnit = RecurringReportIntervalUnit.Week,
                startsAt = DateTimeOffset.UtcNow.AddDays(-1),
                endsAt = DateTimeOffset.UtcNow.AddDays(30),
                note = "Schedule workflow"
            });
        Assert.That(assignmentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var assignment = await ReadAsync<RecurringReportAssignmentDto>(assignmentResponse);
        return new ScheduleScenario(trainer, trainee, assignment);
    }

    private static string TrainerOverrideUrl(ScheduleScenario scenario) =>
        $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/next-report-override";

    private async Task<PendingScheduleState> CreatePendingScheduleStateAsync(ScheduleScenario scenario)
    {
        var overrideResponse = await PostAsJsonWithApiOptionsAsync(
            TrainerOverrideUrl(scenario),
            new { scheduledAt = DateTimeOffset.UtcNow.AddDays(2), expectedScheduleRevision = 0 });
        Assert.That(overrideResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var overridden = await ReadAsync<RecurringReportAssignmentDto>(overrideResponse);

        SetAuthorizationHeader(scenario.Trainee.Id);
        var proposalResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainee/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals",
            new { scheduledAt = DateTimeOffset.UtcNow.AddDays(3), reason = "Pending cleanup", expectedScheduleRevision = 1 });
        Assert.That(proposalResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var proposed = await ReadAsync<RecurringReportAssignmentDto>(proposalResponse);
        SetAuthorizationHeader(scenario.Trainer.Id);

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignmentId = ParseId<RecurringReportAssignment>(scenario.Assignment.Id);
        var changes = await database.RecurringReportScheduleChanges.AsNoTracking()
            .Where(change => change.RecurringReportAssignmentId == assignmentId)
            .ToListAsync();
        var activeOverride = changes.Single(change =>
            change.Kind == RecurringReportScheduleChangeKind.TrainerOverride
            && change.Status == RecurringReportScheduleChangeStatus.Active);
        var pendingProposal = changes.Single(change =>
            change.Kind == RecurringReportScheduleChangeKind.TraineeProposal
            && change.Status == RecurringReportScheduleChangeStatus.Pending);
        Assert.That(overridden.ScheduleRevision, Is.EqualTo(1));
        Assert.That(proposed.ScheduleRevision, Is.EqualTo(1));
        return new PendingScheduleState(activeOverride.Id, pendingProposal.Id);
    }

    private async Task DeleteTemplateAsync(string templateId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = ParseId<ReportTemplate>(templateId);
        var template = await database.ReportTemplates.SingleAsync(candidate => candidate.Id == id);
        template.IsDeleted = true;
        await database.SaveChangesAsync();
    }

    private async Task AssertProposalHistoryVisibleToBothActorsAsync(
        ScheduleScenario scenario,
        IReadOnlyList<ProposalAuditExpectation> expected)
    {
        SetAuthorizationHeader(scenario.Trainer.Id);
        await AssertProposalHistoryAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments",
            scenario,
            expected);

        SetAuthorizationHeader(scenario.Trainee.Id);
        await AssertProposalHistoryAsync(
            "/api/trainee/recurring-report-assignments",
            scenario,
            expected);

        SetAuthorizationHeader(scenario.Trainer.Id);
    }

    private async Task AssertProposalHistoryAsync(
        string url,
        ScheduleScenario scenario,
        IReadOnlyList<ProposalAuditExpectation> expected)
    {
        using var response = await Client.GetAsync(url);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var assignment = document.RootElement.EnumerateArray().Single(item =>
            item.GetProperty("_id").GetString() == scenario.Assignment.Id);
        Assert.That(assignment.TryGetProperty("pendingScheduleProposal", out _), Is.False);
        var history = assignment.GetProperty("scheduleProposalHistory").EnumerateArray().ToArray();
        Assert.That(history, Has.Exactly(expected.Count).Items);

        for (var index = 0; index < expected.Count; index++)
        {
            var actual = history[index];
            Assert.That(actual.GetProperty("_id").GetString(), Is.EqualTo(expected[index].Id));
            Assert.That(actual.GetProperty("assignmentId").GetString(), Is.EqualTo(scenario.Assignment.Id));
            Assert.That(actual.GetProperty("status").GetString(), Is.EqualTo(expected[index].Status.ToString()));
            Assert.That(actual.GetProperty("reason").GetString(), Is.EqualTo(expected[index].Reason));
            Assert.That(actual.GetProperty("scheduleRevision").GetInt32(), Is.EqualTo(expected[index].ScheduleRevision));
            Assert.That(actual.GetProperty("requestedByAccountId").GetString(), Is.EqualTo(scenario.Trainee.Id.ToString()));
            Assert.That(actual.GetProperty("requestedAt").GetDateTimeOffset(), Is.Not.EqualTo(default(System.DateTimeOffset)));
            Assert.That(actual.GetProperty("resolvedByAccountId").GetString(), Is.EqualTo(scenario.Trainer.Id.ToString()));
            Assert.That(actual.GetProperty("resolvedAt").GetDateTimeOffset(), Is.Not.EqualTo(default(System.DateTimeOffset)));
        }
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

    private sealed record ScheduleScenario(User Trainer, User Trainee, RecurringReportAssignmentDto Assignment);

    private sealed record PendingScheduleState(
        Id<RecurringReportScheduleChange> OverrideId,
        Id<RecurringReportScheduleChange> ProposalId);

    private sealed record ProposalAuditExpectation(
        string Id,
        RecurringReportScheduleChangeStatus Status,
        string Reason,
        int ScheduleRevision);
}
