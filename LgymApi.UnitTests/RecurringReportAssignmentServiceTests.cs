using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RecurringReportAssignmentServiceTests
{
    [Test]
    public async Task CreateAsync_CreatesRecurringAssignment_ForOwnedTrainee()
    {
        await using var db = CreateDbContext("recurring-create-success");
        var trainer = CreateUser();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainer.Id);
        db.ReportTemplates.Add(template);
        await db.SaveChangesAsync();

        var service = CreateService(db, trainer.Id, traineeId, ownsTrainee: true);

        var result = await service.CreateAsync(trainer, traineeId, new UpsertRecurringReportAssignmentCommand
        {
            TemplateId = template.Id,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow,
            Note = "weekly check-in"
        });

        result.IsSuccess.Should().BeTrue();
        db.RecurringReportAssignments.Should().ContainSingle();
        db.RecurringReportAssignments.Single().TemplateId.Should().Be(template.Id);
    }

    [Test]
    public async Task CreateAsync_Fails_WhenTraineeIsNotOwnedByTrainer()
    {
        await using var db = CreateDbContext("recurring-create-forbidden");
        var trainer = CreateUser();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainer.Id);
        db.ReportTemplates.Add(template);
        await db.SaveChangesAsync();

        var service = CreateService(db, trainer.Id, traineeId, ownsTrainee: false);

        var result = await service.CreateAsync(trainer, traineeId, new UpsertRecurringReportAssignmentCommand
        {
            TemplateId = template.Id,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow,
        });

        result.IsFailure.Should().BeTrue();
        db.RecurringReportAssignments.Should().BeEmpty();
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_DoesNotCreateNextRequest_BeforeFeedbackRead()
    {
        await using var db = CreateDbContext("recurring-worker-blocked");
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainerId);
        var currentRequest = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = currentRequest.Id,
            ReportRequest = currentRequest,
            TraineeId = traineeId,
            PayloadJson = "{}",
            TrainerFeedbackAddedAt = DateTimeOffset.UtcNow.AddDays(-1),
            TrainerFeedbackReadAt = null
        };
        currentRequest.Submission = submission;
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10),
            IsActive = true,
            CurrentReportRequestId = currentRequest.Id,
            CurrentReportRequest = currentRequest,
            NextEligibleAt = null
        };

        db.ReportTemplates.Add(template);
        db.ReportRequests.Add(currentRequest);
        db.ReportSubmissions.Add(submission);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var service = CreateService(db, trainerId, traineeId, ownsTrainee: true, commandDispatcher: commandDispatcher);

        await service.ProcessDueAssignmentsAsync();

        db.ReportRequests.Should().HaveCount(1);
        await commandDispatcher.DidNotReceiveWithAnyArgs().EnqueueAsync(Arg.Any<ReportRequestCreatedInAppNotificationCommand>());
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_CreatesNextRequest_AfterFeedbackReadAndIntervalElapsed()
    {
        await using var db = CreateDbContext("recurring-worker-success");
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainerId);
        var currentRequest = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = currentRequest.Id,
            ReportRequest = currentRequest,
            TraineeId = traineeId,
            PayloadJson = "{}",
            TrainerFeedbackAddedAt = DateTimeOffset.UtcNow.AddDays(-9),
            TrainerFeedbackReadAt = DateTimeOffset.UtcNow.AddDays(-8)
        };
        currentRequest.Submission = submission;
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-20),
            IsActive = true,
            CurrentReportRequestId = currentRequest.Id,
            CurrentReportRequest = currentRequest,
            NextEligibleAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        db.ReportTemplates.Add(template);
        db.ReportRequests.Add(currentRequest);
        db.ReportSubmissions.Add(submission);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var service = CreateService(db, trainerId, traineeId, ownsTrainee: true, commandDispatcher: commandDispatcher);

        await service.ProcessDueAssignmentsAsync();

        db.ReportRequests.Should().HaveCount(2);
        db.RecurringReportAssignments.Single().CurrentReportRequestId.Should().NotBe(currentRequest.Id);
        await commandDispatcher.Received(1).EnqueueAsync(Arg.Any<ReportRequestCreatedInAppNotificationCommand>());
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_IsIdempotent_WhenCalledTwice()
    {
        await using var db = CreateDbContext("recurring-worker-idempotent");
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainerId);
        var currentRequest = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = currentRequest.Id,
            ReportRequest = currentRequest,
            TraineeId = traineeId,
            PayloadJson = "{}",
            TrainerFeedbackAddedAt = DateTimeOffset.UtcNow.AddDays(-9),
            TrainerFeedbackReadAt = DateTimeOffset.UtcNow.AddDays(-8)
        };
        currentRequest.Submission = submission;
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-20),
            IsActive = true,
            CurrentReportRequestId = currentRequest.Id,
            CurrentReportRequest = currentRequest,
            NextEligibleAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        db.ReportTemplates.Add(template);
        db.ReportRequests.Add(currentRequest);
        db.ReportSubmissions.Add(submission);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var service = CreateService(db, trainerId, traineeId, ownsTrainee: true, commandDispatcher: commandDispatcher);

        await service.ProcessDueAssignmentsAsync();
        await service.ProcessDueAssignmentsAsync();

        db.ReportRequests.Should().HaveCount(2);
        await commandDispatcher.Received(1).EnqueueAsync(Arg.Any<ReportRequestCreatedInAppNotificationCommand>());
    }

    private static RecurringReportAssignmentService CreateService(
        AppDbContext db,
        Id<User> trainerId,
        Id<User> traineeId,
        bool ownsTrainee,
        ICommandDispatcher? commandDispatcher = null)
    {
        var roleRepository = Substitute.For<IRoleRepository>();
        roleRepository.UserHasRoleAsync(trainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var trainerRelationshipRepository = Substitute.For<ITrainerRelationshipRepository>();
        trainerRelationshipRepository
            .FindActiveLinkByTrainerAndTraineeAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(ownsTrainee
                ? new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainerId, TraineeId = traineeId }
                : null);

        return new RecurringReportAssignmentService(new RecurringReportAssignmentServiceDependencies(
            roleRepository,
            trainerRelationshipRepository,
            new ReportingRepository(db),
            new RecurringReportAssignmentRepository(db),
            commandDispatcher ?? Substitute.For<ICommandDispatcher>(),
            new EfUnitOfWork(db)));
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<RecurringReportAssignmentServiceTests>.New():N}")
            .Options);

    private static User CreateUser()
        => new()
        {
            Id = Id<User>.New(),
            Name = "Trainer",
            Email = "trainer@example.com",
            ProfileRank = "Rookie"
        };

    private static ReportTemplate CreateTemplate(Id<User> trainerId)
        => new()
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainerId,
            Name = "Weekly check-in",
            Fields =
            [
                new ReportTemplateField
                {
                    Id = Id<ReportTemplateField>.New(),
                    Key = "summary",
                    Label = "Summary",
                    Order = 1,
                    Type = ReportFieldType.Text,
                    IsRequired = true,
                }
            ]
        };
}
