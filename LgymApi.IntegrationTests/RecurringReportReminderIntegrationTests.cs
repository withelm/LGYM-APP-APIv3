using System.Net;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
public sealed class RecurringReportReminderIntegrationTests : PostgreSqlRecurringReportHttpTestBase
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task DueReminder_DeliveryAndReplay_ShowExactlyOneNotification(bool upcoming)
    {
        var scenario = await SeedScenarioAsync(upcoming);
        var envelopeId = await ProduceAsync();
        await DeliverAsync(envelopeId);
        await AssertVisibleCountAsync(scenario.TraineeId, 1);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var envelope = await database.CommandEnvelopes.SingleAsync(item => item.Id == envelopeId);
            envelope.Status = ActionExecutionStatus.Pending;
            envelope.CompletedAt = null;
            envelope.ProcessingStartedAtUtc = null;
            envelope.NextAttemptAt = null;
            await database.SaveChangesAsync();
        }

        await DeliverAsync(envelopeId);
        await AssertVisibleCountAsync(scenario.TraineeId, 1);
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var notifications = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>().InAppNotifications;
        (await notifications.CountAsync(item => item.RecipientId == scenario.TraineeId)).Should().Be(1);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DueReminder_DeliveredAfterUnlink_IsNotVisible(bool upcoming)
    {
        var scenario = await SeedScenarioAsync(upcoming);
        var envelopeId = await ProduceAsync();
        SetAuthorizationHeader(scenario.TrainerId);
        using var response = await Client.PostAsync($"/api/trainer/trainees/{scenario.TraineeId}/unlink", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await DeliverAsync(envelopeId);
        await AssertVisibleCountAsync(scenario.TraineeId, 0);
    }

    private async Task<(Id<User> TrainerId, Id<User> TraineeId)> SeedScenarioAsync(bool upcoming)
    {
        var trainer = await SeedUserAsync("reminder-trainer", "reminder-trainer@example.test");
        var trainee = await SeedUserAsync("reminder-trainee", "reminder-trainee@example.test");
        var now = DateTimeOffset.UtcNow;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id,
            StartedAt = now.AddDays(-5), StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
        };
        var template = new ReportTemplate { Id = Id<ReportTemplate>.New(), TrainerId = trainer.Id, Name = "Reminder" };
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id,
            TemplateId = template.Id, SourceCoachingEpisodeId = episode.Id,
            IntervalValue = 1, IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = upcoming ? now.AddHours(12) : now.AddDays(-4),
            NextEligibleAt = upcoming ? now.AddHours(12) : null
        };
        if (!upcoming)
        {
            var request = new ReportRequest
            {
                Id = Id<ReportRequest>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id,
                TemplateId = template.Id, SourceCoachingEpisodeId = episode.Id,
                Status = ReportRequestStatus.Pending, CreatedAt = now.AddHours(-49)
            };
            assignment.CurrentReportRequestId = request.Id;
            db.ReportRequests.Add(request);
        }
        db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
        db.CoachingEpisodes.Add(episode);
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id, EpisodeId = episode.Id
        });
        db.ReportTemplates.Add(template);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return (trainer.Id, trainee.Id);
    }

    private async Task<Id<CommandEnvelope>> ProduceAsync()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecurringReportAssignmentService>().ProcessDueAssignmentsAsync();
        }
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatchJob>().ExecuteAsync();
        }
        await using var readScope = Factory.Services.CreateAsyncScope();
        var envelopes = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().CommandEnvelopes.AsNoTracking().ToArrayAsync();
        return envelopes.Should().ContainSingle().Subject.Id;
    }

    private async Task DeliverAsync(Id<CommandEnvelope> envelopeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IActionMessageJob>().ExecuteAsync(envelopeId.ToString());
    }

    private async Task AssertVisibleCountAsync(Id<User> traineeId, int count)
    {
        SetAuthorizationHeader(traineeId);
        using var response = await Client.GetAsync($"/api/{traineeId}/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("items").GetArrayLength().Should().Be(count);
    }
}
