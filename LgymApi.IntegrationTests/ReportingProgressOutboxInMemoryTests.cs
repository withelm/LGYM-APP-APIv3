using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
public sealed class ReportingProgressOutboxInMemoryTests : IntegrationTestBase
{
    private const string AcceptedProgressCommandId =
        "LgymApi.BackgroundWorker.Common.Commands.ReportSubmissionAcceptedProgressCommand";

    [Test]
    public async Task ThreeHttpSubmissions_DispatchDeliveryAndReplay_PersistThreeMeasurementsAndReceipts()
    {
        var (_, traineeId, requestIds) = await SeedMeasurementRequestsAsync();
        SetAuthorizationHeader(traineeId);

        foreach (var requestId in requestIds)
        {
            using var response = await Client.PostAsJsonAsync(
                $"/api/trainee/report-requests/{requestId}/submit",
                new
                {
                    answers = new
                    {
                        measurements = new
                        {
                            BodyWeight = new { value = 82.4, unit = MeasurementUnits.Kilograms.ToString() }
                        }
                    }
                });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using (var dispatchScope = Factory.Services.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider
                .GetRequiredService<ICommittedIntentDispatchJob>()
                .ExecuteAsync();
        }

        var envelopeIds = await ReadAcceptedEnvelopeIdsAsync();
        envelopeIds.Should().HaveCount(3);
        await DeliverAsync(envelopeIds);
        await AssertPersistedOutcomeAsync(traineeId, 3);

        await ResetForReplayAsync(envelopeIds);
        await DeliverAsync(envelopeIds);
        await AssertPersistedOutcomeAsync(traineeId, 3);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AcceptedReportProgress_DeliveredAfterUnlink_RemainsVisibleOnlyWithinEpisodeScope(bool trainerInitiated)
    {
        var (originalTrainerId, traineeId, requestIds) = await SeedMeasurementRequestsAsync();
        var requestId = requestIds[0];
        SetAuthorizationHeader(traineeId);

        using (var response = await Client.PostAsJsonAsync(
                   $"/api/trainee/report-requests/{requestId}/submit",
                   new
                   {
                       answers = new
                       {
                           measurements = new
                           {
                               BodyWeight = new { value = 82.4, unit = MeasurementUnits.Kilograms.ToString() }
                           }
                       }
                   }))
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        SetAuthorizationHeader(trainerInitiated ? originalTrainerId : traineeId);
        var unlinkUrl = trainerInitiated
            ? $"/api/trainer/trainees/{traineeId}/unlink"
            : "/api/trainee/trainer/detach";
        using (var unlinkResponse = await Client.PostAsync(unlinkUrl, null))
        {
            unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using (var dispatchScope = Factory.Services.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<ICommittedIntentDispatchJob>().ExecuteAsync();
        }

        await DeliverAsync(await ReadAcceptedEnvelopeIdsAsync());
        await AssertPersistedOutcomeAsync(traineeId, 1);

        SetAuthorizationHeader(originalTrainerId);
        var formerTrainerHistory = await Client.GetFromJsonAsync<MeasurementsHistoryResponse>(
            $"/api/measurements/{traineeId}/getHistory?bodyPart=BodyWeight&unit=Kilograms");
        formerTrainerHistory!.Measurements.Select(measurement => measurement.Value).Should().Equal(82.4);

        SetAuthorizationHeader(traineeId);
        using (var manualResponse = await Client.PostAsJsonAsync("/api/measurements/add", new
               {
                   bodyPart = BodyParts.BodyWeight.ToString(),
                   value = 91.0,
                   unit = MeasurementUnits.Kilograms.ToString(),
                   createdAt = DateTimeOffset.UtcNow.AddYears(-1)
               }))
        {
            manualResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        SetAuthorizationHeader(originalTrainerId);
        var historyAfterBackdatedManualWrite = await Client.GetFromJsonAsync<MeasurementsHistoryResponse>(
            $"/api/measurements/{traineeId}/getHistory?bodyPart=BodyWeight&unit=Kilograms");
        historyAfterBackdatedManualWrite!.Measurements.Select(measurement => measurement.Value).Should().Equal(82.4);

        var successor = await SeedUserAsync(
            name: "in-memory-progress-successor",
            email: "in-memory-progress-successor@example.com");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var episode = new CoachingEpisode
            {
                Id = Id<CoachingEpisode>.New(),
                TrainerId = successor.Id,
                TraineeId = traineeId,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation,
                AuthorizationRevision = 0
            };
            database.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(database, successor.Id));
            database.CoachingEpisodes.Add(episode);
            database.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = successor.Id,
                TraineeId = traineeId,
                EpisodeId = episode.Id
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(successor.Id);
        var successorHistory = await Client.GetFromJsonAsync<MeasurementsHistoryResponse>(
            $"/api/measurements/{traineeId}/getHistory?bodyPart=BodyWeight&unit=Kilograms");
        successorHistory!.Measurements.Select(measurement => measurement.Value).Should().BeEquivalentTo(new[] { 82.4, 91.0 });
    }

    private async Task<(Id<User> TrainerId, Id<User> TraineeId, IReadOnlyList<Id<ReportRequest>> RequestIds)> SeedMeasurementRequestsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainer = await TestDataFactory.SeedUserAsync(
            database,
            "in-memory-progress-trainer",
            "in-memory-progress-trainer@example.com",
            "password123");
        var trainee = await TestDataFactory.SeedUserAsync(
            database,
            "in-memory-progress-trainee",
            "in-memory-progress-trainee@example.com",
            "password123");
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Progress check-in"
        };
        template.Fields.Add(new ReportTemplateField
        {
            Id = Id<ReportTemplateField>.New(),
            TemplateId = template.Id,
            Key = "measurements",
            Label = "Measurements",
            Type = ReportFieldType.Measurements,
            IsRequired = true,
            Order = 0,
            ModuleConfig = """{"measurementTypes":["BodyWeight"]}"""
        });
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation,
            AuthorizationRevision = 0
        };
        var requests = Enumerable.Range(0, 3)
            .Select(_ => new ReportRequest
            {
                Id = Id<ReportRequest>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                TemplateId = template.Id,
                Status = ReportRequestStatus.Pending,
                SourceCoachingEpisodeId = episode.Id
            })
            .ToArray();

        database.ReportTemplates.Add(template);
        database.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
        database.CoachingEpisodes.Add(episode);
        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            EpisodeId = episode.Id
        });
        database.ReportRequests.AddRange(requests);
        await database.SaveChangesAsync();

        return (trainer.Id, trainee.Id, requests.Select(request => request.Id).ToArray());
    }

    private async Task<IReadOnlyList<Id<CommandEnvelope>>> ReadAcceptedEnvelopeIdsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await database.CommandEnvelopes
            .AsNoTracking()
            .Where(envelope => envelope.CommandTypeFullName == AcceptedProgressCommandId)
            .Select(envelope => envelope.Id)
            .ToArrayAsync();
    }

    private async Task DeliverAsync(IEnumerable<Id<CommandEnvelope>> envelopeIds)
    {
        foreach (var envelopeId in envelopeIds)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider
                .GetRequiredService<IActionMessageJob>()
                .ExecuteAsync(envelopeId.ToString());
        }
    }

    private async Task ResetForReplayAsync(IEnumerable<Id<CommandEnvelope>> envelopeIds)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var envelopes = await database.CommandEnvelopes
            .Where(envelope => envelopeIds.Contains(envelope.Id))
            .ToArrayAsync();

        foreach (var envelope in envelopes)
        {
            envelope.Status = ActionExecutionStatus.Pending;
            envelope.CompletedAt = null;
            envelope.ProcessingStartedAtUtc = null;
            envelope.NextAttemptAt = null;
        }

        await database.SaveChangesAsync();
    }

    private async Task AssertPersistedOutcomeAsync(Id<User> traineeId, int expectedCount)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submissionIds = await database.ReportSubmissions
            .AsNoTracking()
            .Where(submission => submission.TraineeId == traineeId)
            .Select(submission => submission.Id)
            .ToArrayAsync();
        var receiptSubmissionIds = await database.ReportSubmissionProgressReceipts
            .AsNoTracking()
            .Select(receipt => receipt.ReportSubmissionId)
            .ToArrayAsync();
        var measurements = await database.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.UserId == traineeId)
            .ToArrayAsync();

        submissionIds.Should().HaveCount(expectedCount);
        receiptSubmissionIds.Should().BeEquivalentTo(submissionIds);
        measurements.Should().HaveCount(expectedCount);
        measurements.Should().OnlyContain(measurement =>
            measurement.BodyPart == BodyParts.BodyWeight
            && measurement.Value == 82.4
            && measurement.Unit == MeasurementUnits.Kilograms.ToString());
    }

    private sealed class MeasurementsHistoryResponse
    {
        public List<MeasurementResponse> Measurements { get; set; } = [];
    }

    private sealed class MeasurementResponse
    {
        public double Value { get; set; }
    }
}
