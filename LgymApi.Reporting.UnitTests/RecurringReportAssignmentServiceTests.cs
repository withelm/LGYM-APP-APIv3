using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed partial class RecurringReportAssignmentServiceTests
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

        var result = await service.CreateAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), new UpsertRecurringReportAssignmentCommand
        {
            TemplateId = template.Id,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow,
            Note = "weekly check-in"
        });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(db.RecurringReportAssignments, Has.Exactly(1).Items);
        Assert.That(db.RecurringReportAssignments.Single().TemplateId, Is.EqualTo(template.Id));
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

        var result = await service.CreateAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), new UpsertRecurringReportAssignmentCommand
        {
            TemplateId = template.Id,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow,
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.That(db.RecurringReportAssignments, Is.Empty);
    }

    [Test]
    public async Task PauseAsync_WhenTrainerNoLongerOwnsTrainee_ReturnsNotFoundWithoutMutatingAssignment()
    {
        await using var db = CreateDbContext("recurring-pause-not-owned");
        var trainer = CreateUser();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainer.Id);
        var assignment = CreateAssignment(trainer.Id, traineeId, template.Id);
        db.ReportTemplates.Add(template);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = CreateService(db, trainer.Id, traineeId, ownsTrainee: false);
        var result = await service.PauseAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), assignment.Id);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ReportingNotFoundError>());
        Assert.That(db.RecurringReportAssignments.Single().IsActive, Is.True);
    }

    [Test]
    public async Task PauseAsync_WhenCallerIsNotTrainer_ReturnsForbiddenWithoutMutatingAssignment()
    {
        await using var db = CreateDbContext("recurring-pause-not-trainer");
        var caller = CreateUser();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(caller.Id);
        var assignment = CreateAssignment(caller.Id, traineeId, template.Id);
        db.ReportTemplates.Add(template);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = CreateService(db, caller.Id, traineeId, ownsTrainee: true, isTrainer: false);
        var result = await service.PauseAsync(ReportingTestData.Account(caller.Id), ReportingTestData.AccountId(traineeId), assignment.Id);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ReportingForbiddenError>());
        Assert.That(db.RecurringReportAssignments.Single().IsActive, Is.True);
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_DoesNotCreateNextRequest_BeforeFeedbackRead()
    {
        await using var db = CreateRelationalDbContext();
        var trainer = CreateUser();
        var trainee = CreateUser(Id<User>.New(), "Trainee", "trainee-blocked@example.com");
        var trainerId = trainer.Id;
        var traineeId = trainee.Id;
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

        db.Users.AddRange(trainer, trainee);
        db.ReportTemplates.Add(template);
        db.ReportRequests.Add(currentRequest);
        db.ReportSubmissions.Add(submission);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var commandOutboxWriter = CreateOutboxWriter();
        var service = CreateService(db, trainerId, traineeId, ownsTrainee: true, commandOutboxWriter: commandOutboxWriter);

        await service.ProcessDueAssignmentsAsync();

        Assert.That(db.ReportRequests, Has.Exactly(1).Items);
        await commandOutboxWriter.DidNotReceiveWithAnyArgs().StageAsync(
            Arg.Any<ReportRequestCreatedInAppNotificationCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_CreatesNextRequest_AfterFeedbackReadAndIntervalElapsed()
    {
        await using var db = CreateRelationalDbContext();
        var trainer = CreateUser();
        var trainee = CreateUser(Id<User>.New(), "Trainee", "trainee-success@example.com");
        var trainerId = trainer.Id;
        var traineeId = trainee.Id;
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

        db.Users.AddRange(trainer, trainee);
        db.ReportTemplates.Add(template);
        db.ReportRequests.Add(currentRequest);
        db.ReportSubmissions.Add(submission);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var commandOutboxWriter = CreateOutboxWriter();
        var service = CreateService(db, trainerId, traineeId, ownsTrainee: true, commandOutboxWriter: commandOutboxWriter);

        await service.ProcessDueAssignmentsAsync();

        Assert.That(db.ReportRequests, Has.Exactly(2).Items);
        Assert.That(db.RecurringReportAssignments.Single().CurrentReportRequestId, Is.Not.EqualTo(currentRequest.Id));
        await commandOutboxWriter.Received(1).StageAsync(
            Arg.Any<ReportRequestCreatedInAppNotificationCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_IsIdempotent_WhenCalledTwice()
    {
        await using var db = CreateRelationalDbContext();
        var trainer = CreateUser();
        var trainee = CreateUser(Id<User>.New(), "Trainee", "trainee-idempotent@example.com");
        var trainerId = trainer.Id;
        var traineeId = trainee.Id;
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

        db.Users.AddRange(trainer, trainee);
        db.ReportTemplates.Add(template);
        db.ReportRequests.Add(currentRequest);
        db.ReportSubmissions.Add(submission);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var commandOutboxWriter = CreateOutboxWriter();
        var service = CreateService(db, trainerId, traineeId, ownsTrainee: true, commandOutboxWriter: commandOutboxWriter);

        await service.ProcessDueAssignmentsAsync();
        await service.ProcessDueAssignmentsAsync();

        Assert.That(db.ReportRequests, Has.Exactly(2).Items);
        await commandOutboxWriter.Received(1).StageAsync(
            Arg.Any<ReportRequestCreatedInAppNotificationCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_UpdatesAssignmentFields()
    {
        await using var db = CreateDbContext("recurring-update-success");
        var trainer = CreateUser();
        var traineeId = Id<User>.New();
        var oldTemplate = CreateTemplate(trainer.Id);
        var newTemplate = CreateTemplate(trainer.Id);
        var assignment = CreateAssignment(trainer.Id, traineeId, oldTemplate.Id);
        db.ReportTemplates.AddRange(oldTemplate, newTemplate);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = CreateService(db, trainer.Id, traineeId, ownsTrainee: true);
        var startsAt = DateTimeOffset.UtcNow.AddDays(2);

        var result = await service.UpdateAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), assignment.Id, new UpsertRecurringReportAssignmentCommand
        {
            TemplateId = newTemplate.Id,
            IntervalValue = 2,
            IntervalUnit = RecurringReportIntervalUnit.Month,
            StartsAt = startsAt,
            EndsAt = startsAt.AddDays(30),
            Note = "updated note"
        });

        Assert.That(result.IsSuccess, Is.True);
        var stored = db.RecurringReportAssignments.Single();
        Assert.That(stored.TemplateId, Is.EqualTo(newTemplate.Id));
        Assert.That(stored.IntervalValue, Is.EqualTo(2));
        Assert.That(stored.IntervalUnit, Is.EqualTo(RecurringReportIntervalUnit.Month));
        Assert.That(stored.Note, Is.EqualTo("updated note"));
    }

    [Test]
    public async Task PauseAndResumeAsync_ToggleActiveState()
    {
        await using var db = CreateDbContext("recurring-pause-resume");
        var trainer = CreateUser();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainer.Id);
        var assignment = CreateAssignment(trainer.Id, traineeId, template.Id);
        db.ReportTemplates.Add(template);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = CreateService(db, trainer.Id, traineeId, ownsTrainee: true);

        Assert.That((await service.PauseAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), assignment.Id)).IsSuccess, Is.True);
        Assert.That(db.RecurringReportAssignments.Single().IsActive, Is.False);

        Assert.That((await service.ResumeAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), assignment.Id)).IsSuccess, Is.True);
        Assert.That(db.RecurringReportAssignments.Single().IsActive, Is.True);
    }

    [Test]
    public async Task DeleteAsync_SoftDeletesAssignment()
    {
        await using var db = CreateDbContext("recurring-delete");
        var trainer = CreateUser();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainer.Id);
        var assignment = CreateAssignment(trainer.Id, traineeId, template.Id);
        db.ReportTemplates.Add(template);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = CreateService(db, trainer.Id, traineeId, ownsTrainee: true);

        var result = await service.DeleteAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), assignment.Id);

        Assert.That(result.IsSuccess, Is.True);
        var stored = db.RecurringReportAssignments.IgnoreQueryFilters().Single();
        Assert.That(stored.IsDeleted, Is.True);
        Assert.That(stored.IsActive, Is.False);
    }

    [Test]
    public async Task UpdateAsync_WhenTemplateDeleted_ReturnsFailure()
    {
        await using var db = CreateDbContext("recurring-update-template-deleted");
        var trainer = CreateUser();
        var traineeId = Id<User>.New();
        var template = CreateTemplate(trainer.Id);
        template.IsDeleted = true;
        var activeTemplate = CreateTemplate(trainer.Id);
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainer.Id,
            TraineeId = traineeId,
            TemplateId = activeTemplate.Id,
            Template = activeTemplate,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10),
            IsActive = true
        };

        db.ReportTemplates.AddRange(template, activeTemplate);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = CreateService(db, trainer.Id, traineeId, ownsTrainee: true);

        var result = await service.UpdateAsync(ReportingTestData.Account(trainer.Id, true), ReportingTestData.AccountId(traineeId), assignment.Id, new UpsertRecurringReportAssignmentCommand
        {
            TemplateId = template.Id,
            IntervalValue = 2,
            IntervalUnit = RecurringReportIntervalUnit.Month,
            StartsAt = DateTimeOffset.UtcNow,
            Note = "updated"
        });

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ReportingNotFoundError>());
    }

    private static RecurringReportAssignmentService CreateService(
        AppDbContext db,
        Id<User> trainerId,
        Id<User> traineeId,
        bool ownsTrainee,
        ICommandOutboxWriter? commandOutboxWriter = null,
        bool isTrainer = true)
    {
        var relationshipAccess = Substitute.For<IReportingRelationshipAccessPersistence>();
        var assignments = db.RecurringReportAssignments.IgnoreQueryFilters().ToList();
        var activeEpisodeId = assignments.Select(assignment => assignment.SourceCoachingEpisodeId)
            .FirstOrDefault(episodeId => episodeId.HasValue)
            ?? Id<CoachingEpisode>.New();
        if (assignments.Count > 0)
        {
            if (db.Database.IsRelational() && !db.CoachingEpisodes.Any(episode => episode.Id == activeEpisodeId))
            {
                db.CoachingEpisodes.Add(new CoachingEpisode
                {
                    Id = activeEpisodeId,
                    TrainerId = trainerId,
                    TraineeId = traineeId,
                    StartedAt = DateTimeOffset.UtcNow.AddYears(-1),
                    StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
                });
            }
            foreach (var assignment in assignments.Where(assignment => assignment.SourceCoachingEpisodeId is null))
            {
                assignment.SourceCoachingEpisodeId = activeEpisodeId;
            }
            db.SaveChanges();
        }
        var activeEpisode = new ReportingActiveRelationshipFact(activeEpisodeId, 0);
        relationshipAccess
            .GetAccessAsync(ReportingTestData.AccountId(trainerId), ReportingTestData.AccountId(traineeId), Arg.Any<CancellationToken>())
            .Returns(new ReportingRelationshipAccessFact(ownsTrainee, [], ownsTrainee ? activeEpisode : null));
        relationshipAccess
            .StageActiveEpisodeRevisionCheckAsync(activeEpisode, Arg.Any<CancellationToken>())
            .Returns(true);

        return new RecurringReportAssignmentService(
            new ReportTemplatePersistenceRepository(db),
            new ReportRequestSubmissionPersistenceRepository(db),
            new RecurringReportAssignmentPersistenceRepository(db),
            relationshipAccess,
            ReportingTestData.Mapper(),
            commandOutboxWriter ?? CreateOutboxWriter(),
            new EfUnitOfWork(db),
            NullLogger<RecurringReportAssignmentService>.Instance);
    }

    private static ICommandOutboxWriter CreateOutboxWriter()
    {
        var writer = Substitute.For<ICommandOutboxWriter>();
        writer.StageAsync(
                Arg.Any<ReportRequestCreatedInAppNotificationCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(new CommandEnvelopeStageResult("test-envelope", false));
        return writer;
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<RecurringReportAssignmentServiceTests>.New():N}")
            .Options);

    private static AppDbContext CreateRelationalDbContext()
    {
        var database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options);
        database.Database.OpenConnection();
        database.Database.EnsureCreated();
        return database;
    }

    private static User CreateUser()
        => CreateUser(Id<User>.New(), "Trainer", "trainer@example.com");

    private static User CreateUser(Id<User> id, string name, string email)
        => new() { Id = id, Name = name, Email = email, ProfileRank = "Rookie" };

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

    private static RecurringReportAssignment CreateAssignment(Id<User> trainerId, Id<User> traineeId, Id<ReportTemplate> templateId)
        => new()
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = templateId,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-5),
            IsActive = true,
            Note = "weekly",
            NextEligibleAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
}
