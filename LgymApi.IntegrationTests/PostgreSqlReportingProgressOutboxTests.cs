using System.Reflection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;
using LgymApi.Application.WorkoutProgress.ReportingIntegration;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.Resources;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
internal sealed class PostgreSqlReportingProgressOutboxTests : PostgreSqlIntegrationTestBase
{
    private const string AcceptedProgressCommandId =
        "LgymApi.BackgroundWorker.Common.Commands.ReportSubmissionAcceptedProgressCommand";

    private const string AcceptedProgressCommandTypeName =
        "LgymApi.Application.Reporting.Contracts.BackgroundCommands.ReportSubmissionAcceptedProgressCommand";

    private const string CommandOutboxWriterTypeName =
        "LgymApi.Application.Platform.Contracts.BackgroundCommands.ICommandOutboxWriter";

    [Test]
    public async Task SubmitReportRequestAsync_WithMeasurements_CommitsSubmissionRequestStateAndAcceptedProgressEnvelopeTogether()
    {
        var scenario = await CreatePendingMeasurementScenarioAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();
            var result = await reportingService.SubmitReportRequestAsync(
                scenario.Account,
                scenario.RequestId,
                CreateMeasurementSubmissionCommand());

            result.IsSuccess.Should().BeTrue();
        }

        var persisted = await ReadSubmissionStateAsync(scenario.RequestId);

        Assert.Multiple(() =>
        {
            persisted.Submission.Should().NotBeNull();
            persisted.RequestStatus.Should().Be(ReportRequestStatus.Submitted);
            persisted.AcceptedProgressEnvelopes.Should().ContainSingle(
                "the accepted-progress command must be staged before the Reporting unit of work commits");
            persisted.AcceptedProgressEnvelopes[0].CommandTypeFullName.Should().Be(AcceptedProgressCommandId);
            persisted.AcceptedProgressEnvelopes[0].PayloadJson.Should().Contain(persisted.Submission!.Id.ToString());
        });
    }

    [Test]
    public async Task SubmitReportRequestAsync_WhenSecondSubmissionIsRejected_DoesNotCreateAnotherAcceptedProgressEnvelope()
    {
        var scenario = await CreatePendingMeasurementScenarioAsync();
        var submissionCommand = CreateMeasurementSubmissionCommand();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var reportingService = scope.ServiceProvider.GetRequiredService<IReportingService>();
            var first = await reportingService.SubmitReportRequestAsync(scenario.Account, scenario.RequestId, submissionCommand);
            var envelopeCountAfterFirstSubmission = await CountAcceptedProgressEnvelopesAsync();
            var second = await reportingService.SubmitReportRequestAsync(scenario.Account, scenario.RequestId, submissionCommand);
            var envelopeCountAfterDuplicate = await CountAcceptedProgressEnvelopesAsync();

            Assert.Multiple(() =>
            {
                first.IsSuccess.Should().BeTrue();
                envelopeCountAfterFirstSubmission.Should().Be(1);
                second.IsFailure.Should().BeTrue();
                second.Error.Should().BeOfType<InvalidReportingError>();
                second.Error.Message.Should().Be(Messages.ReportRequestNotPending);
                envelopeCountAfterDuplicate.Should().Be(envelopeCountAfterFirstSubmission);
            });
        }
    }

    [Test]
    public async Task CommittedAcceptedProgressEnvelope_IsDispatchedAndDeliveredIdempotentlyWithoutHangfire()
    {
        var scenario = await CreatePendingMeasurementScenarioAsync();

        await using (var submissionScope = Factory.Services.CreateAsyncScope())
        {
            var reportingService = submissionScope.ServiceProvider.GetRequiredService<IReportingService>();
            var result = await reportingService.SubmitReportRequestAsync(
                scenario.Account,
                scenario.RequestId,
                CreateMeasurementSubmissionCommand());

            result.IsSuccess.Should().BeTrue();
        }

        var stagedEnvelope = (await ReadSubmissionStateAsync(scenario.RequestId))
            .AcceptedProgressEnvelopes
            .Should()
            .ContainSingle()
            .Subject;

        await using (var dispatchScope = Factory.Services.CreateAsyncScope())
        {
            var dispatchJob = dispatchScope.ServiceProvider.GetRequiredService<ICommittedIntentDispatchJob>();
            await dispatchJob.ExecuteAsync();
        }

        var dispatchedEnvelope = await ReadEnvelopeAsync(stagedEnvelope.Id);
        Assert.Multiple(() =>
        {
            dispatchedEnvelope.DispatchedAt.Should().NotBeNull("the committed intent must be scheduled after its database commit");
            dispatchedEnvelope.SchedulerJobId.Should().StartWith(
                "noop-command-",
                "Testing uses the registered no-op action scheduler instead of a Hangfire server");
            dispatchedEnvelope.Status.Should().Be(ActionExecutionStatus.Pending);
        });

        await using (var deliveryScope = Factory.Services.CreateAsyncScope())
        {
            var job = deliveryScope.ServiceProvider.GetRequiredService<IActionMessageJob>();
            await job.ExecuteAsync(stagedEnvelope.Id.ToString());
        }

        await ResetEnvelopeForReplayAsync(stagedEnvelope.Id);

        await using (var replayScope = Factory.Services.CreateAsyncScope())
        {
            var job = replayScope.ServiceProvider.GetRequiredService<IActionMessageJob>();
            await job.ExecuteAsync(stagedEnvelope.Id.ToString());
        }

        var delivery = await ReadDeliveryStateAsync(stagedEnvelope.Id, scenario.Trainee.Id);
        Assert.Multiple(() =>
        {
            delivery.Envelope.Status.Should().Be(ActionExecutionStatus.Completed);
            delivery.Measurements.Should().ContainSingle();
            delivery.Measurements[0].BodyPart.Should().Be(BodyParts.BodyWeight);
            delivery.Measurements[0].Value.Should().Be(82.4);
            delivery.Measurements[0].Unit.Should().Be(MeasurementUnits.Kilograms.ToString());
            delivery.Receipts.Should().ContainSingle();
            stagedEnvelope.PayloadJson.Should().Contain(delivery.Receipts[0].ReportSubmissionId.ToString());
        });
    }

    [Test]
    public async Task SubmitReportRequestApi_WithMeasurements_PreservesLegacySuccessResponseShape()
    {
        var scenario = await CreatePendingMeasurementScenarioAsync();
        await AuthenticateAsAsync(scenario.Trainee);

        var response = await Client.PostAsJsonAsync(
            $"/api/trainee/report-requests/{scenario.RequestId}/submit",
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

        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responseBody = responseJson.RootElement;
        ReadRequiredString(responseBody, "_id").Should().NotBeNullOrWhiteSpace();
        responseBody.TryGetProperty("id", out _).Should().BeFalse("the established submission DTO exposes _id");
        ReadRequiredString(responseBody, "reportRequestId").Should().Be(scenario.RequestId.ToString());
        ReadRequiredString(responseBody, "traineeId").Should().Be(scenario.Trainee.Id.ToString());
        responseBody.TryGetProperty("answers", out var answers).Should().BeTrue();
        answers.ValueKind.Should().Be(JsonValueKind.Object);
        answers.TryGetProperty("measurements", out _).Should().BeTrue();
        responseBody.TryGetProperty("request", out _).Should().BeTrue("the established submission DTO includes its request");
    }

    [Test]
    public async Task ThreeHttpSubmissions_DispatchAndDeliverThreeDistinctReceipts()
    {
        var scenario = await CreatePendingMeasurementScenarioAsync();
        var requestIds = await CreateSiblingRequestsAsync(scenario.RequestId, 2);
        requestIds = [scenario.RequestId, .. requestIds];
        await AuthenticateAsAsync(scenario.Trainee);

        foreach (var requestId in requestIds)
        {
            var response = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{requestId}/submit", new
            {
                answers = new { measurements = new { BodyWeight = new { value = 82.4, unit = "Kilograms" } } }
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using (var dispatchScope = Factory.Services.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<ICommittedIntentDispatchJob>().ExecuteAsync();
        }

        var envelopeIds = await ReadAcceptedEnvelopeIdsAsync();
        envelopeIds.Should().HaveCount(3);
        foreach (var envelopeId in envelopeIds)
        {
            await using var deliveryScope = Factory.Services.CreateAsyncScope();
            await deliveryScope.ServiceProvider.GetRequiredService<IActionMessageJob>().ExecuteAsync(envelopeId.ToString());
        }

        await AssertReceiptAndMeasurementCountAsync(scenario.Trainee.Id, 3, 3);
    }

    [Test]
    public async Task ForcedFailure_AfterStagingSubmissionAndAcceptedProgressCommand_RollsBackBothFromAFreshContext()
    {
        var scenario = await CreatePendingMeasurementScenarioAsync();
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = scenario.RequestId,
            TraineeId = scenario.Trainee.Id,
            PayloadJson = "{}"
        };

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unitOfWork = new EfUnitOfWork(database);
            var request = await database.ReportRequests.SingleAsync(candidate => candidate.Id == scenario.RequestId);

            try
            {
                await using var transaction = await unitOfWork.BeginTransactionAsync();
                request.Status = ReportRequestStatus.Submitted;
                await database.ReportSubmissions.AddAsync(submission);
                await StageAcceptedProgressCommandAsync(scope.ServiceProvider, submission, scenario.Trainee.Id, scenario.RequestId);
                await unitOfWork.SaveChangesAsync();

                throw new ForcedFailureAfterOutboxStagingException();
            }
            catch (ForcedFailureAfterOutboxStagingException)
            {
            }
        }

        var persisted = await ReadSubmissionStateAsync(scenario.RequestId);

        Assert.Multiple(() =>
        {
            persisted.Submission.Should().BeNull();
            persisted.RequestStatus.Should().Be(ReportRequestStatus.Pending);
            persisted.AcceptedProgressEnvelopes.Should().BeEmpty();
        });
    }

    [Test]
    public async Task DistinctSubmissions_KeepManualAndEqualMeasurements()
    {
        var trainee = await SeedUserAsync("receipt-trainee", "receipt-trainee@example.com");
        var events = new[]
        {
            CreateProgressEvent(trainee.Id, "00000000-0000-0000-0000-000000000101", "00000000-0000-0000-0000-000000000201", new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)),
            CreateProgressEvent(trainee.Id, "00000000-0000-0000-0000-000000000102", "00000000-0000-0000-0000-000000000202", new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)),
            CreateProgressEvent(trainee.Id, "00000000-0000-0000-0000-000000000103", "00000000-0000-0000-0000-000000000203", new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero))
        };

        await using (var setupScope = Factory.Services.CreateAsyncScope())
        {
            var database = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.Measurements.Add(new Measurement
            {
                Id = Id<Measurement>.New(), UserId = trainee.Id, BodyPart = BodyParts.BodyWeight,
                Unit = MeasurementUnits.Kilograms.ToString(), Value = 82.4,
                CreatedAt = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
            });
            await database.SaveChangesAsync();
        }

        foreach (var progressEvent in events)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IReportSubmissionAcceptedProgressConsumer>()
                .ConsumeAsync(progressEvent);
            result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        }

        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await assertionDatabase.ReportSubmissionProgressReceipts.CountAsync()).Should().Be(3);
        var measurements = await assertionDatabase.Measurements.Where(value => value.UserId == trainee.Id).ToArrayAsync();
        measurements.Should().HaveCount(4, "manual and equal same-day values must not suppress distinct submissions");
        measurements.Count(value => value.Value == 82.4).Should().Be(4);
    }

    [Test]
    public async Task AcceptedMeasurements_PreserveExactUtcInstantsUnitsValuesAndBodyPartsAcrossOffsets()
    {
        var trainee = await SeedUserAsync("receipt-fidelity", "receipt-fidelity@example.com");
        var cases = new[]
        {
            new MeasurementFidelityCase(
                "00000000-0000-0000-0000-000000000111",
                "00000000-0000-0000-0000-000000000211",
                new DateTimeOffset(2026, 7, 20, 12, 15, 0, TimeSpan.Zero),
                BodyParts.BodyWeight,
                MeasurementUnits.Kilograms,
                82.4),
            new MeasurementFidelityCase(
                "00000000-0000-0000-0000-000000000112",
                "00000000-0000-0000-0000-000000000212",
                new DateTimeOffset(2026, 7, 21, 1, 15, 0, TimeSpan.FromHours(5.5)),
                BodyParts.Waist,
                MeasurementUnits.Centimeters,
                87.125),
            new MeasurementFidelityCase(
                "00000000-0000-0000-0000-000000000113",
                "00000000-0000-0000-0000-000000000213",
                new DateTimeOffset(2026, 7, 20, 22, 45, 0, TimeSpan.FromHours(-4)),
                BodyParts.BodyFat,
                MeasurementUnits.Percent,
                18.6),
            new MeasurementFidelityCase(
                "00000000-0000-0000-0000-000000000114",
                "00000000-0000-0000-0000-000000000214",
                new DateTimeOffset(2026, 7, 20, 18, 45, 0, TimeSpan.FromHours(-2.5)),
                BodyParts.Chest,
                MeasurementUnits.Centimeters,
                101.25)
        };

        foreach (var testCase in cases)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var progressEvent = CreateProgressEvent(
                trainee.Id,
                testCase.EventId,
                testCase.SubmissionId,
                testCase.ObservedAt,
                new ReportSubmissionAcceptedMeasurement(testCase.BodyPart, testCase.Value, testCase.Unit));
            var result = await scope.ServiceProvider.GetRequiredService<IReportSubmissionAcceptedProgressConsumer>()
                .ConsumeAsync(progressEvent);
            result.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        }

        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.ReportSubmissionProgressReceipts.AsNoTracking().CountAsync()).Should().Be(cases.Length);
        var persisted = await database.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.UserId == trainee.Id)
            .ToArrayAsync();
        persisted.Should().HaveCount(cases.Length);
        foreach (var expected in cases)
        {
            persisted.Should().ContainSingle(measurement =>
                measurement.CreatedAt == expected.ObservedAt.ToUniversalTime()
                && measurement.BodyPart == expected.BodyPart
                && measurement.Unit == expected.Unit.ToString()
                && measurement.Value == expected.Value);
        }
    }

    [Test]
    public async Task ConcurrentSameSubmission_OneCommitWinsAndFreshScopeRetryIsDuplicate()
    {
        var trainee = await SeedUserAsync("receipt-race", "receipt-race@example.com");
        var progressEvent = CreateProgressEvent(trainee.Id, "00000000-0000-0000-0000-000000000104", "00000000-0000-0000-0000-000000000204", DateTimeOffset.UtcNow);
        var gate = new ReceiptLookupGate();
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var first = CreateGatedConsumer(firstScope.ServiceProvider, gate);
        var second = CreateGatedConsumer(secondScope.ServiceProvider, gate);

        var firstTask = first.ConsumeAsync(progressEvent);
        var secondTask = second.ConsumeAsync(progressEvent);
        await gate.BothReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        gate.Release.TrySetResult(true).Should().BeTrue();
        var outcomes = await Task.WhenAll(CaptureAsync(firstTask), CaptureAsync(secondTask));

        outcomes.Count(outcome => outcome.Outcome == "Applied").Should().Be(1);
        var loser = outcomes.Single(outcome => outcome.Error is not null).Error!;
        loser.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        loser.ConstraintName.Should().Be("IX_ReportSubmissionProgressReceipts_ReportSubmissionId");
        await using var retryScope = Factory.Services.CreateAsyncScope();
        var retry = await retryScope.ServiceProvider.GetRequiredService<IReportSubmissionAcceptedProgressConsumer>().ConsumeAsync(progressEvent);
        retry.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate);
        await AssertReceiptAndMeasurementCountAsync(trainee.Id, 1, 1);
    }

    [Test]
    public async Task ConcurrentActionMessageJobs_SameSubmission_LoserRetriesFromFreshScopeWithoutDuplicateMeasurements()
    {
        var trainee = await SeedUserAsync("receipt-job-race", "receipt-job-race@example.com");
        var gate = new ReceiptLookupGate();
        using var gatedFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReportSubmissionAcceptedProgressPersistence>();
            services.AddSingleton(gate);
            services.AddScoped<ReportSubmissionAcceptedProgressPersistenceRepository>();
            services.AddScoped<IReportSubmissionAcceptedProgressPersistence>(provider =>
                new GatedReceiptPersistence(
                    provider.GetRequiredService<ReportSubmissionAcceptedProgressPersistenceRepository>(),
                    provider.GetRequiredService<ReceiptLookupGate>()));
        }));
        var submissionId = Id<ReportSubmission>.New().ToString();
        var observedAt = new DateTimeOffset(2026, 7, 20, 23, 45, 0, TimeSpan.FromHours(-2.5));
        var measurements = new[]
        {
            new ReportSubmissionAcceptedProgressMeasurement(BodyParts.BodyWeight, 82.4, MeasurementUnits.Kilograms),
            new ReportSubmissionAcceptedProgressMeasurement(BodyParts.Waist, 87.1, MeasurementUnits.Centimeters),
            new ReportSubmissionAcceptedProgressMeasurement(BodyParts.BodyFat, 18.6, MeasurementUnits.Percent)
        };
        var firstEnvelopeId = await PersistAcceptedProgressEnvelopeAsync(
            gatedFactory.Services,
            CreateAcceptedProgressPayload(trainee.Id, Id<ReportSubmissionAcceptedProgressPayload>.New().ToString(), submissionId, observedAt, measurements));
        var secondEnvelopeId = await PersistAcceptedProgressEnvelopeAsync(
            gatedFactory.Services,
            CreateAcceptedProgressPayload(trainee.Id, Id<ReportSubmissionAcceptedProgressPayload>.New().ToString(), submissionId, observedAt, measurements));

        await using var firstScope = gatedFactory.Services.CreateAsyncScope();
        await using var secondScope = gatedFactory.Services.CreateAsyncScope();
        var firstTask = CaptureJobAsync(firstScope.ServiceProvider.GetRequiredService<IActionMessageJob>(), firstEnvelopeId);
        var secondTask = CaptureJobAsync(secondScope.ServiceProvider.GetRequiredService<IActionMessageJob>(), secondEnvelopeId);
        await gate.BothReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        gate.Release.TrySetResult(true).Should().BeTrue();
        var attempts = await Task.WhenAll(firstTask, secondTask);

        attempts.Count(attempt => attempt.Error is null).Should().Be(1);
        attempts.Count(attempt => attempt.Error is InvalidOperationException).Should().Be(1);
        var envelopes = await ReadEnvelopesWithLogsAsync(gatedFactory.Services, firstEnvelopeId, secondEnvelopeId);
        envelopes.Count(envelope => envelope.Status == ActionExecutionStatus.Completed).Should().Be(1);
        var failedEnvelope = envelopes.Single(envelope => envelope.Status == ActionExecutionStatus.Failed);
        failedEnvelope.LastAttemptAt.Should().NotBeNull();
        failedEnvelope.NextAttemptAt.Should().NotBeNull().And.BeAfter(failedEnvelope.LastAttemptAt!.Value);
        var failedHandlerLog = failedEnvelope.ExecutionLogs.Single(log =>
            log.ActionType == ActionExecutionLogType.HandlerExecution
            && log.Status == ActionExecutionStatus.Failed);
        failedHandlerLog.ErrorDetails.Should().Contain(PostgresErrorCodes.UniqueViolation);
        failedHandlerLog.ErrorDetails.Should().Contain("IX_ReportSubmissionProgressReceipts_ReportSubmissionId");

        await MakeEnvelopeRetryDueAsync(gatedFactory.Services, failedEnvelope.Id);
        await using (var retryScope = gatedFactory.Services.CreateAsyncScope())
        {
            await retryScope.ServiceProvider.GetRequiredService<IActionMessageJob>().ExecuteAsync(failedEnvelope.Id.ToString());
        }

        var retriedEnvelope = (await ReadEnvelopesWithLogsAsync(gatedFactory.Services, failedEnvelope.Id)).Single();
        retriedEnvelope.Status.Should().Be(ActionExecutionStatus.Completed);
        retriedEnvelope.NextAttemptAt.Should().BeNull();
        await AssertReceiptAndMeasurementsAsync(gatedFactory.Services, trainee.Id, observedAt, measurements);
    }

    [Test]
    public async Task SaveFailure_RollsBackReceiptAndMeasurementsThenFreshRetryApplies()
    {
        var trainee = await SeedUserAsync("receipt-rollback", "receipt-rollback@example.com");
        var progressEvent = CreateProgressEvent(trainee.Id, "00000000-0000-0000-0000-000000000105", "00000000-0000-0000-0000-000000000205", DateTimeOffset.UtcNow);

        await using (var failingScope = Factory.Services.CreateAsyncScope())
        {
            var unitOfWork = failingScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var consumer = new ReportSubmissionAcceptedProgressConsumer(
                new InvalidRowPersistence(
                    failingScope.ServiceProvider.GetRequiredService<IReportSubmissionAcceptedProgressPersistence>(),
                    failingScope.ServiceProvider.GetRequiredService<AppDbContext>()),
                unitOfWork);
            await Assert.ThrowsAsync<DbUpdateException>(() => consumer.ConsumeAsync(progressEvent));
        }

        await AssertReceiptAndMeasurementCountAsync(trainee.Id, 0, 0);
        await using var retryScope = Factory.Services.CreateAsyncScope();
        var retry = await retryScope.ServiceProvider.GetRequiredService<IReportSubmissionAcceptedProgressConsumer>().ConsumeAsync(progressEvent);
        retry.Outcome.Should().Be(ReportSubmissionAcceptedProgressConsumeOutcome.Applied);
        await AssertReceiptAndMeasurementCountAsync(trainee.Id, 1, 1);
    }

    private async Task<PendingMeasurementScenario> CreatePendingMeasurementScenarioAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainer = await TestDataFactory.SeedUserAsync(
            database,
            "reporting-outbox-trainer",
            "reporting-outbox-trainer@example.com",
            "password123");
        var trainee = await TestDataFactory.SeedUserAsync(
            database,
            "reporting-outbox-trainee",
            "reporting-outbox-trainee@example.com",
            "password123");
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Progress check-in"
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Status = ReportRequestStatus.Pending
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

        database.ReportTemplates.Add(template);
        database.ReportRequests.Add(request);
        await database.SaveChangesAsync();

        return new PendingMeasurementScenario(
            trainee,
            new AuthenticatedAccountContext(trainee.Id.Rebind<AccountReference>(), null, [], [], false, false),
            request.Id);
    }

    private async Task<IReadOnlyList<Id<ReportRequest>>> CreateSiblingRequestsAsync(Id<ReportRequest> requestId, int count)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var source = await database.ReportRequests.SingleAsync(value => value.Id == requestId);
        var requests = Enumerable.Range(0, count).Select(_ => new ReportRequest
        {
            Id = Id<ReportRequest>.New(), TrainerId = source.TrainerId, TraineeId = source.TraineeId,
            TemplateId = source.TemplateId, Status = ReportRequestStatus.Pending
        }).ToArray();
        database.ReportRequests.AddRange(requests);
        await database.SaveChangesAsync();
        return requests.Select(value => value.Id).ToArray();
    }

    private async Task<IReadOnlyList<Id<CommandEnvelope>>> ReadAcceptedEnvelopeIdsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await database.CommandEnvelopes.AsNoTracking()
            .Where(value => value.CommandTypeFullName == AcceptedProgressCommandId)
            .Select(value => value.Id).ToArrayAsync();
    }

    private async Task<PersistedSubmissionState> ReadSubmissionStateAsync(Id<ReportRequest> requestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await database.ReportRequests
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == requestId);
        var submission = await database.ReportSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ReportRequestId == requestId);
        var acceptedProgressEnvelopes = await database.CommandEnvelopes
            .AsNoTracking()
            .Where(envelope => envelope.CommandTypeFullName == AcceptedProgressCommandId)
            .ToArrayAsync();

        return new PersistedSubmissionState(request.Status, submission, acceptedProgressEnvelopes);
    }

    private async Task<int> CountAcceptedProgressEnvelopesAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await database.CommandEnvelopes.CountAsync(envelope => envelope.CommandTypeFullName == AcceptedProgressCommandId);
    }

    private async Task<CommandEnvelope> ReadEnvelopeAsync(Id<CommandEnvelope> envelopeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await database.CommandEnvelopes
            .AsNoTracking()
            .SingleAsync(envelope => envelope.Id == envelopeId);
    }

    private async Task<DeliveredAcceptedProgressState> ReadDeliveryStateAsync(
        Id<CommandEnvelope> envelopeId,
        Id<User> traineeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var envelope = await database.CommandEnvelopes
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == envelopeId);
        var measurements = await database.Measurements
            .AsNoTracking()
            .Where(candidate => candidate.UserId == traineeId && candidate.BodyPart == BodyParts.BodyWeight)
            .ToArrayAsync();
        var receipts = await database.ReportSubmissionProgressReceipts
            .AsNoTracking()
            .ToArrayAsync();

        return new DeliveredAcceptedProgressState(envelope, measurements, receipts);
    }

    private async Task ResetEnvelopeForReplayAsync(Id<CommandEnvelope> envelopeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var envelope = await database.CommandEnvelopes.SingleAsync(candidate => candidate.Id == envelopeId);
        envelope.Status = ActionExecutionStatus.Pending;
        envelope.CompletedAt = null;
        envelope.ProcessingStartedAtUtc = null;
        envelope.NextAttemptAt = null;
        await database.SaveChangesAsync();
    }

    private async Task AuthenticateAsAsync(User user)
    {
        var response = await Client.PostAsJsonAsync("/api/login", new
        {
            name = user.Name,
            password = "password123"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var loginJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ReadRequiredString(loginJson.RootElement, "token"));
    }

    private static SubmitReportRequestCommand CreateMeasurementSubmissionCommand()
    {
        using var document = JsonDocument.Parse("""
            {
              "measurements": {
                "BodyWeight": { "value": 82.4, "unit": "Kilograms" }
              }
            }
            """);

        return new SubmitReportRequestCommand
        {
            Answers = document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal)
        };
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out var property).Should().BeTrue($"response must contain {propertyName}");
        property.ValueKind.Should().Be(JsonValueKind.String, $"{propertyName} must remain a JSON string");
        var value = property.GetString();
        value.Should().NotBeNullOrWhiteSpace($"{propertyName} must not be empty");
        return value!;
    }

    private static async Task StageAcceptedProgressCommandAsync(
        IServiceProvider serviceProvider,
        ReportSubmission submission,
        Id<User> traineeId,
        Id<ReportRequest> requestId)
    {
        var applicationAssembly = typeof(IReportingService).Assembly;
        var commandType = applicationAssembly.GetType(AcceptedProgressCommandTypeName);
        var outboxWriterType = typeof(ICommandOutboxWriter).Assembly.GetType(CommandOutboxWriterTypeName);

        commandType.Should().NotBeNull("#386 requires a Reporting-owned accepted-progress command contract");
        outboxWriterType.Should().NotBeNull("#386 requires a stage-only command outbox writer");

        var command = Activator.CreateInstance(commandType!)
            ?? throw new InvalidOperationException($"Could not create {AcceptedProgressCommandTypeName}.");
        var eventProperty = commandType!.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(property => property.PropertyType == typeof(ReportSubmissionAcceptedProgressPayload));
        eventProperty.Should().NotBeNull("the accepted-progress command must carry the Reporting-owned payload contract");
        eventProperty!.SetValue(command, CreateAcceptedProgressEvent(submission, traineeId, requestId));

        var outboxWriter = serviceProvider.GetService(outboxWriterType!)
            ?? throw new InvalidOperationException($"No {CommandOutboxWriterTypeName} is registered.");
        var stageMethod = outboxWriterType!.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(method => method.Name == "StageAsync" && method.IsGenericMethodDefinition);
        stageMethod.Should().NotBeNull("the stage-only outbox writer must expose StageAsync<TCommand>");

        var invocation = stageMethod!.MakeGenericMethod(commandType).Invoke(
            outboxWriter,
            [command, CancellationToken.None]);
        if (invocation is not Task task)
        {
            throw new InvalidOperationException("ICommandOutboxWriter.StageAsync<TCommand> must return Task.");
        }

        await task;
    }

    private static ReportSubmissionAcceptedProgressPayload CreateAcceptedProgressEvent(
        ReportSubmission submission,
        Id<User> traineeId,
        Id<ReportRequest> requestId)
    {
        var acceptedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

        return new ReportSubmissionAcceptedProgressPayload(
            ReportSubmissionAcceptedProgressPayload.CurrentSchemaVersion,
            Id<ReportSubmissionAcceptedProgressPayload>.New().ToString(),
            submission.Id.ToString(),
            requestId.ToString(),
            submission.Id.ToString(),
            traineeId.Rebind<AccountReference>(),
            acceptedAt,
            acceptedAt,
            [new ReportSubmissionAcceptedProgressMeasurement(BodyParts.BodyWeight, 82.4, MeasurementUnits.Kilograms)]);
    }

    private static ReportSubmissionAcceptedProgressEvent CreateProgressEvent(
        Id<User> traineeId,
        string eventId,
        string submissionId,
        DateTimeOffset observedAt,
        params ReportSubmissionAcceptedMeasurement[] measurements)
        => new(
            ReportSubmissionAcceptedProgressEvent.CurrentSchemaVersion,
            eventId,
            submissionId,
            Id<ReportSubmissionAcceptedProgressEvent>.New().ToString(),
            Id<ReportSubmissionAcceptedProgressEvent>.New().ToString(),
            traineeId.Rebind<AccountReference>(),
            observedAt,
            observedAt,
            measurements.Length == 0
                ? [new ReportSubmissionAcceptedMeasurement(BodyParts.BodyWeight, 82.4, MeasurementUnits.Kilograms)]
                : measurements);

    private static ReportSubmissionAcceptedProgressPayload CreateAcceptedProgressPayload(
        Id<User> traineeId,
        string eventId,
        string submissionId,
        DateTimeOffset observedAt,
        IReadOnlyList<ReportSubmissionAcceptedProgressMeasurement> measurements)
        => new(
            ReportSubmissionAcceptedProgressPayload.CurrentSchemaVersion,
            eventId,
            submissionId,
            Id<ReportSubmissionAcceptedProgressPayload>.New().ToString(),
            Id<ReportSubmissionAcceptedProgressPayload>.New().ToString(),
            traineeId.Rebind<AccountReference>(),
            observedAt,
            observedAt,
            measurements);

    private static async Task<Id<CommandEnvelope>> PersistAcceptedProgressEnvelopeAsync(
        IServiceProvider services,
        ReportSubmissionAcceptedProgressPayload payload)
    {
        await using var scope = services.CreateAsyncScope();
        var command = new ReportSubmissionAcceptedProgressCommand { Event = payload };
        var request = new CommandEnvelopeRequest(
            AcceptedProgressCommandId,
            JsonSerializer.Serialize(command, SharedSerializationOptions.Current));
        var receipt = await scope.ServiceProvider.GetRequiredService<ICommandEnvelopeRuntime>().PersistAsync(request);
        receipt.EnvelopeId.Should().NotBeNull();
        Id<CommandEnvelope>.TryParse(receipt.EnvelopeId!, out var envelopeId).Should().BeTrue();
        return envelopeId;
    }

    private static async Task<JobAttempt> CaptureJobAsync(IActionMessageJob job, Id<CommandEnvelope> envelopeId)
    {
        try
        {
            await job.ExecuteAsync(envelopeId.ToString());
            return new JobAttempt(envelopeId, null);
        }
        catch (Exception exception)
        {
            return new JobAttempt(envelopeId, exception);
        }
    }

    private static async Task<IReadOnlyList<CommandEnvelope>> ReadEnvelopesWithLogsAsync(
        IServiceProvider services,
        params Id<CommandEnvelope>[] envelopeIds)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await database.CommandEnvelopes
            .AsNoTracking()
            .Include(envelope => envelope.ExecutionLogs)
            .Where(envelope => envelopeIds.Contains(envelope.Id))
            .ToArrayAsync();
    }

    private static async Task MakeEnvelopeRetryDueAsync(
        IServiceProvider services,
        Id<CommandEnvelope> envelopeId)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var envelope = await database.CommandEnvelopes.SingleAsync(candidate => candidate.Id == envelopeId);
        envelope.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await database.SaveChangesAsync();
    }

    private static async Task AssertReceiptAndMeasurementsAsync(
        IServiceProvider services,
        Id<User> traineeId,
        DateTimeOffset observedAt,
        IReadOnlyList<ReportSubmissionAcceptedProgressMeasurement> expectedMeasurements)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.ReportSubmissionProgressReceipts.AsNoTracking().CountAsync()).Should().Be(1);
        var persisted = await database.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.UserId == traineeId)
            .ToArrayAsync();
        persisted.Should().HaveCount(expectedMeasurements.Count);
        foreach (var expected in expectedMeasurements)
        {
            persisted.Should().ContainSingle(measurement =>
                measurement.BodyPart == expected.BodyPart
                && measurement.Unit == expected.Unit.ToString()
                && measurement.Value == expected.Value
                && measurement.CreatedAt == observedAt.ToUniversalTime());
        }
    }

    private static ReportSubmissionAcceptedProgressConsumer CreateGatedConsumer(
        IServiceProvider services,
        ReceiptLookupGate gate)
        => new(
            new GatedReceiptPersistence(
                services.GetRequiredService<IReportSubmissionAcceptedProgressPersistence>(), gate),
            services.GetRequiredService<IUnitOfWork>());

    private static async Task<RaceAttempt> CaptureAsync(Task<ReportSubmissionAcceptedProgressConsumeResult> task)
    {
        try
        {
            return new((await task).Outcome.ToString(), null);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException)
        {
            return new(nameof(DbUpdateException), postgresException);
        }
    }

    private async Task AssertReceiptAndMeasurementCountAsync(Id<User> traineeId, int receipts, int measurements)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.ReportSubmissionProgressReceipts.CountAsync()).Should().Be(receipts);
        (await database.Measurements.CountAsync(value => value.UserId == traineeId)).Should().Be(measurements);
    }

    private sealed record PendingMeasurementScenario(
        User Trainee,
        AuthenticatedAccountContext Account,
        Id<ReportRequest> RequestId);

    private sealed record PersistedSubmissionState(
        ReportRequestStatus RequestStatus,
        ReportSubmission? Submission,
        IReadOnlyList<CommandEnvelope> AcceptedProgressEnvelopes);

    private sealed record DeliveredAcceptedProgressState(
        CommandEnvelope Envelope,
        IReadOnlyList<Measurement> Measurements,
        IReadOnlyList<ReportSubmissionProgressReceipt> Receipts);

    private sealed class ReceiptLookupGate
    {
        private int _reached;
        public TaskCompletionSource<bool> BothReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitAsync()
        {
            if (Interlocked.Increment(ref _reached) == 2)
            {
                BothReached.TrySetResult(true);
            }

            await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private sealed class GatedReceiptPersistence(
        IReportSubmissionAcceptedProgressPersistence inner,
        ReceiptLookupGate gate) : IReportSubmissionAcceptedProgressPersistence
    {
        public async Task<bool> HasReceiptAsync(Id<ReportSubmission> reportSubmissionId, CancellationToken cancellationToken = default)
        {
            var exists = await inner.HasReceiptAsync(reportSubmissionId, cancellationToken);
            await gate.WaitAsync();
            return exists;
        }

        public Task AddReceiptAsync(AcceptedReportSubmissionReceiptPersistenceModel receipt, CancellationToken cancellationToken = default)
            => inner.AddReceiptAsync(receipt, cancellationToken);

        public Task AddAsync(AcceptedReportMeasurementPersistenceModel measurement, CancellationToken cancellationToken = default)
            => inner.AddAsync(measurement, cancellationToken);
    }

    private sealed class InvalidRowPersistence(
        IReportSubmissionAcceptedProgressPersistence inner,
        AppDbContext database) : IReportSubmissionAcceptedProgressPersistence
    {
        public Task<bool> HasReceiptAsync(Id<ReportSubmission> reportSubmissionId, CancellationToken cancellationToken = default)
            => inner.HasReceiptAsync(reportSubmissionId, cancellationToken);

        public Task AddReceiptAsync(AcceptedReportSubmissionReceiptPersistenceModel receipt, CancellationToken cancellationToken = default)
            => inner.AddReceiptAsync(receipt, cancellationToken);

        public async Task AddAsync(AcceptedReportMeasurementPersistenceModel measurement, CancellationToken cancellationToken = default)
        {
            await inner.AddAsync(measurement, cancellationToken);
            database.Measurements.Add(new Measurement
            {
                Id = Id<Measurement>.New(), UserId = Id<User>.New(), BodyPart = BodyParts.BodyWeight,
                Unit = MeasurementUnits.Kilograms.ToString(), Value = 1, CreatedAt = measurement.CreatedAt
            });
        }
    }

    private sealed record RaceAttempt(string Outcome, PostgresException? Error);

    private sealed record JobAttempt(Id<CommandEnvelope> EnvelopeId, Exception? Error);

    private sealed record MeasurementFidelityCase(
        string EventId,
        string SubmissionId,
        DateTimeOffset ObservedAt,
        BodyParts BodyPart,
        MeasurementUnits Unit,
        double Value);

    private sealed class ForcedFailureAfterOutboxStagingException : Exception;
}
