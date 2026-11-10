using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
public sealed class RecurringReportScheduleIntegrationTests : PostgreSqlRecurringReportHttpTestBase
{
    [Test]
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
        invalidOffsetResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var overrideResponse = await PostAsJsonWithApiOptionsAsync(
            TrainerOverrideUrl(scenario),
            new { scheduledAt = overrideAt, expectedScheduleRevision = 0 });
        overrideResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var overridden = await ReadAsync<RecurringReportAssignmentDto>(overrideResponse);
        overridden.ScheduleRevision.Should().Be(1);
        overridden.EffectiveNextRecurringRequest.ScheduledAt.Should().BeCloseTo(
            overrideAt.ToUniversalTime(),
            TimeSpan.FromMicroseconds(1));

        var staleResponse = await PostAsJsonWithApiOptionsAsync(
            TrainerOverrideUrl(scenario),
            new { scheduledAt = overrideAt.AddHours(1), expectedScheduleRevision = 0 });
        staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        SetAuthorizationHeader(scenario.Trainee.Id);
        var proposalAt = overrideAt.AddDays(1);
        var proposalResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainee/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals",
            new { scheduledAt = proposalAt, reason = "Travel day", expectedScheduleRevision = 1 });
        proposalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposed = await ReadAsync<RecurringReportAssignmentDto>(proposalResponse);
        proposed.ScheduleRevision.Should().Be(1);
        proposed.PendingScheduleProposal.Should().NotBeNull();
        proposed.PendingScheduleProposal!.Reason.Should().Be("Travel day");
        proposed.EffectiveNextRecurringRequest.ScheduledAt.Should().BeCloseTo(
            overrideAt.ToUniversalTime(),
            TimeSpan.FromMicroseconds(1));

        SetAuthorizationHeader(scenario.Trainer.Id);
        SetIdempotencyKey("schedule-workflow-accept");
        using var acceptResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals/{proposed.PendingScheduleProposal.Id}/accept",
            content: null);
        ClearIdempotencyKey();
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await ReadAsync<RecurringReportAssignmentDto>(acceptResponse);
        accepted.ScheduleRevision.Should().Be(2);
        accepted.PendingScheduleProposal.Should().BeNull();
        accepted.EffectiveNextRecurringRequest.ScheduledAt.Should().BeCloseTo(
            proposalAt.ToUniversalTime(),
            TimeSpan.FromMicroseconds(1));

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
        rejectedProposalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingRejection = await ReadAsync<RecurringReportAssignmentDto>(rejectedProposalResponse);
        pendingRejection.PendingScheduleProposal.Should().NotBeNull();

        SetAuthorizationHeader(scenario.Trainer.Id);
        SetIdempotencyKey("schedule-workflow-reject");
        using var rejectResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/next-report-proposals/{pendingRejection.PendingScheduleProposal!.Id}/reject",
            content: null);
        ClearIdempotencyKey();
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

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
        requestNowResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var requested = await ReadAsync<RecurringReportAssignmentDto>(requestNowResponse);
        requested.ScheduleRevision.Should().Be(3);
        requested.PendingScheduleProposal.Should().BeNull();
        requested.EffectiveNextRecurringRequest.Status.Should().Be(NextRecurringReportRequestStatus.Unscheduled);

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var changes = await database.RecurringReportScheduleChanges
            .AsNoTracking()
            .Where(change => change.RecurringReportAssignmentId == ParseId<RecurringReportAssignment>(scenario.Assignment.Id))
            .ToListAsync();
        changes.Should().ContainSingle(change =>
            change.Kind == RecurringReportScheduleChangeKind.TraineeProposal
            && change.Status == RecurringReportScheduleChangeStatus.Approved);
        changes.Should().ContainSingle(change =>
            change.Kind == RecurringReportScheduleChangeKind.TrainerOverride
            && change.Status == RecurringReportScheduleChangeStatus.Superseded);
        changes.Should().ContainSingle(change =>
            change.Kind == RecurringReportScheduleChangeKind.TrainerOverride
            && change.Status == RecurringReportScheduleChangeStatus.Consumed);
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        templateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
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
        assignmentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var assignment = await ReadAsync<RecurringReportAssignmentDto>(assignmentResponse);
        return new ScheduleScenario(trainer, trainee, assignment);
    }

    private static string TrainerOverrideUrl(ScheduleScenario scenario) =>
        $"/api/trainer/trainees/{scenario.Trainee.Id}/recurring-report-assignments/{scenario.Assignment.Id}/next-report-override";

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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var assignment = document.RootElement.EnumerateArray().Single(item =>
            item.GetProperty("_id").GetString() == scenario.Assignment.Id);
        assignment.TryGetProperty("pendingScheduleProposal", out _).Should().BeFalse();
        var history = assignment.GetProperty("scheduleProposalHistory").EnumerateArray().ToArray();
        history.Should().HaveCount(expected.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            var actual = history[index];
            actual.GetProperty("_id").GetString().Should().Be(expected[index].Id);
            actual.GetProperty("assignmentId").GetString().Should().Be(scenario.Assignment.Id);
            actual.GetProperty("status").GetString().Should().Be(expected[index].Status.ToString());
            actual.GetProperty("reason").GetString().Should().Be(expected[index].Reason);
            actual.GetProperty("scheduleRevision").GetInt32().Should().Be(expected[index].ScheduleRevision);
            actual.GetProperty("requestedByAccountId").GetString().Should().Be(scenario.Trainee.Id.ToString());
            actual.GetProperty("requestedAt").GetDateTimeOffset().Should().NotBe(default);
            actual.GetProperty("resolvedByAccountId").GetString().Should().Be(scenario.Trainer.Id.ToString());
            actual.GetProperty("resolvedAt").GetDateTimeOffset().Should().NotBe(default);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) where T : notnull =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private static Id<TEntity> ParseId<TEntity>(string value)
    {
        Id<TEntity>.TryParse(value, out var id).Should().BeTrue();
        return id;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record ScheduleScenario(User Trainer, User Trainee, RecurringReportAssignmentDto Assignment);

    private sealed record ProposalAuditExpectation(
        string Id,
        RecurringReportScheduleChangeStatus Status,
        string Reason,
        int ScheduleRevision);
}
