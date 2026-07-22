using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportingServiceAcceptedProgressOutboxTests
{
    [Test]
    public async Task SubmitReportRequestAsync_StagesOnlyLegacyValidMeasurementCandidatesBeforeCommitAndPreservesNotification()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var template = CreateTemplate(
            CreateMeasurementsField("first", """{ "measurementTypes": ["weight", "chest", "waist", "bodyFat"] }"""),
            CreateMeasurementsField("later", """{ "measurementTypes": ["bodyWeight", "thighs"] }"""));
        var request = CreateRequest(requestId, traineeId, trainerId, template);
        var reportingRepository = Substitute.For<IReportingRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var commandOutboxWriter = Substitute.For<ICommandOutboxWriter>();
        var stagedBeforeCommit = false;
        var committed = false;
        ReportSubmission? addedSubmission = null;

        reportingRepository.FindRequestByIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(request);
        reportingRepository.AddSubmissionAsync(Arg.Any<ReportSubmission>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                addedSubmission = callInfo.Arg<ReportSubmission>();
                return Task.CompletedTask;
            });
        commandOutboxWriter.StageAsync(Arg.Any<ReportSubmissionAcceptedProgressCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stagedBeforeCommit = true;
                return Task.FromResult(new CommandEnvelopeStageResult(null, false));
            });
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stagedBeforeCommit.Should().BeTrue("the accepted-progress envelope must commit atomically with the submission");
                committed = true;
                return Task.FromResult(1);
            });
        commandDispatcher.EnqueueAsync(Arg.Any<ReportSubmissionCreatedInAppNotificationCommand>())
            .Returns(_ =>
            {
                committed.Should().BeTrue("the existing notification remains a post-commit enqueue");
                return Task.CompletedTask;
            });

        var service = CreateService(
            reportingRepository,
            unitOfWork,
            commandDispatcher,
            commandOutboxWriter);

        var result = await service.SubmitReportRequestAsync(
            CreateUser(traineeId),
            requestId,
            new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>
                {
                    ["first"] = ParseJson("""
                    {
                      "weight": { "value": 82.4, "unit": "Kilograms" },
                      "chest": { "value": 101.2, "unit": "Centimeters" },
                      "waist": { "value": 87.1, "unit": "Kilograms" },
                      "bodyFat": { "value": 0, "unit": "Percentages" }
                    }
                    """),
                    ["later"] = ParseJson("""
                    {
                      "bodyWeight": { "value": 81.7, "unit": "Kilograms" },
                      "thighs": { "value": 60.0, "unit": "Centimeters" }
                    }
                    """)
                }
            });

        result.IsSuccess.Should().BeTrue();
        addedSubmission.Should().NotBeNull();
        var expectedMeasurements = new[]
        {
            new ReportSubmissionAcceptedMeasurement(BodyParts.BodyWeight, 82.4, MeasurementUnits.Kilograms),
            new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.2, MeasurementUnits.Centimeters),
            new ReportSubmissionAcceptedMeasurement(BodyParts.Thigh, 60.0, MeasurementUnits.Centimeters)
        };
        await commandOutboxWriter.Received(1).StageAsync(
            Arg.Is<ReportSubmissionAcceptedProgressCommand>(command =>
                command.Event.Validate().IsValid
                && command.Event.SchemaVersion == ReportSubmissionAcceptedProgressEvent.CurrentSchemaVersion
                && command.Event.ReportSubmissionId == addedSubmission!.Id.ToString()
                && command.Event.CorrelationId == requestId.ToString()
                && command.Event.CausationId == addedSubmission.Id.ToString()
                && command.Event.TraineeId == traineeId
                && command.Event.ObservedAt == command.Event.AcceptedAt
                && command.Event.ObservedAt.Offset == TimeSpan.Zero
                && command.Event.Measurements.SequenceEqual(expectedMeasurements)),
            Arg.Any<CancellationToken>());
        await commandDispatcher.Received(1).EnqueueAsync(Arg.Is<ReportSubmissionCreatedInAppNotificationCommand>(command =>
            command.SubmissionId == addedSubmission.Id
            && command.TrainerId == trainerId
            && command.TraineeId == traineeId
            && command.TemplateName == template.Name));
    }

    [Test]
    public async Task SubmitReportRequestAsync_WithEmptyMeasurements_AcceptsSubmissionWithoutStagingAcceptedProgressCommand()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var template = CreateTemplate(
            new ReportTemplateField
            {
                Id = Id<ReportTemplateField>.New(),
                Key = "feedback",
                Label = "Feedback",
                Type = ReportFieldType.Text,
                IsRequired = true,
                Order = 1
            },
            CreateMeasurementsField("measurements", """{ "measurementTypes": ["weight", "waist"] }"""));
        var request = CreateRequest(requestId, traineeId, trainerId, template);
        var reportingRepository = Substitute.For<IReportingRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var commandOutboxWriter = Substitute.For<ICommandOutboxWriter>();

        reportingRepository.FindRequestByIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(request);
        reportingRepository.AddSubmissionAsync(Arg.Any<ReportSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = CreateService(
            reportingRepository,
            unitOfWork,
            commandDispatcher,
            commandOutboxWriter);

        var result = await service.SubmitReportRequestAsync(
            CreateUser(traineeId),
            requestId,
            new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>
                {
                    ["feedback"] = ParseJson("\"complete\""),
                    ["measurements"] = ParseJson("{}")
                }
            });

        result.IsSuccess.Should().BeTrue();
        await commandOutboxWriter.DidNotReceive().StageAsync(
            Arg.Any<ReportSubmissionAcceptedProgressCommand>(),
            Arg.Any<CancellationToken>());
        await commandDispatcher.Received(1).EnqueueAsync(Arg.Is<ReportSubmissionCreatedInAppNotificationCommand>(command =>
            command.TrainerId == trainerId
            && command.TraineeId == traineeId
            && command.TemplateName == template.Name));
    }

    [Test]
    public async Task SubmitReportRequestAsync_WhenAcceptedProgressStagingFails_DoesNotCommitOrEnqueueNotification()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var template = CreateTemplate(CreateMeasurementsField("measurements", """{ "measurementTypes": ["weight"] }"""));
        var reportingRepository = Substitute.For<IReportingRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var commandOutboxWriter = Substitute.For<ICommandOutboxWriter>();
        var expectedException = new InvalidOperationException("Outbox staging failed.");

        reportingRepository.FindRequestByIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(CreateRequest(requestId, traineeId, trainerId, template));
        reportingRepository.AddSubmissionAsync(Arg.Any<ReportSubmission>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        commandOutboxWriter.StageAsync(Arg.Any<ReportSubmissionAcceptedProgressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CommandEnvelopeStageResult>(expectedException));

        var service = CreateService(reportingRepository, unitOfWork, commandDispatcher, commandOutboxWriter);

        var action = () => service.SubmitReportRequestAsync(
            CreateUser(traineeId),
            requestId,
            new SubmitReportRequestCommand
            {
                Answers = new Dictionary<string, JsonElement>
                {
                    ["measurements"] = ParseJson("""{ "weight": { "value": 82.4, "unit": "Kilograms" } }""")
                }
            });

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedException.Message);
        _ = unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await commandDispatcher.DidNotReceive().EnqueueAsync(Arg.Any<ReportSubmissionCreatedInAppNotificationCommand>());
    }

    private static ReportingService CreateService(
        IReportingRepository reportingRepository,
        IUnitOfWork unitOfWork,
        ICommandDispatcher commandDispatcher,
        ICommandOutboxWriter commandOutboxWriter)
    {
        var dependencies = Substitute.For<IReportingServiceDependencies>();
        dependencies.ReportingRepository.Returns(reportingRepository);
        dependencies.UnitOfWork.Returns(unitOfWork);
        dependencies.CommandDispatcher.Returns(commandDispatcher);
        dependencies.CommandOutboxWriter.Returns(commandOutboxWriter);
        dependencies.ReportSubmissionAcceptedProgressCommandFactory.Returns(new ReportSubmissionAcceptedProgressCommandFactory());
        dependencies.RoleRepository.Returns(Substitute.For<IRoleRepository>());
        dependencies.TrainerRelationshipRepository.Returns(Substitute.For<ITrainerRelationshipRepository>());
        dependencies.RecurringReportAssignmentRepository.Returns(Substitute.For<IRecurringReportAssignmentRepository>());
        dependencies.PhotoStorageProvider.Returns(Substitute.For<IPhotoStorageProvider>());
        dependencies.PhotoUploadInitTracker.Returns(Substitute.For<IPhotoUploadInitTracker>());
        dependencies.Logger.Returns(Substitute.For<ILogger<ReportingService>>());
        dependencies.PhotoStorageOptions.Returns(new PhotoStorageOptions());

        return new ReportingService(dependencies);
    }

    private static ReportTemplate CreateTemplate(params ReportTemplateField[] fields)
        => new()
        {
            Id = Id<ReportTemplate>.New(),
            Name = "Progress check-in",
            TrainerId = Id<User>.New(),
            Fields = fields
        };

    private static ReportTemplateField CreateMeasurementsField(string key, string moduleConfig)
        => new()
        {
            Id = Id<ReportTemplateField>.New(),
            Key = key,
            Label = key,
            Type = ReportFieldType.Measurements,
            IsRequired = false,
            Order = 2,
            ModuleConfig = moduleConfig
        };

    private static ReportRequest CreateRequest(
        Id<ReportRequest> requestId,
        Id<User> traineeId,
        Id<User> trainerId,
        ReportTemplate template)
        => new()
        {
            Id = requestId,
            TraineeId = traineeId,
            TrainerId = trainerId,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Pending
        };

    private static User CreateUser(Id<User> userId)
        => new()
        {
            Id = userId,
            Name = "Trainee",
            Email = "trainee@example.com",
            ProfileRank = "Rookie"
        };

    private static JsonElement ParseJson(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
