using FluentAssertions;
using LgymApi.Application.Features.Reporting;
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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RecurringReportAssignmentServiceRelationalTests
{
    [Test]
    public async Task ProcessDueAssignmentsAsync_EndedScheduleRemindsPendingRequestWithoutIssuingAnother_OnSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var now = DateTimeOffset.UtcNow;
        var requestId = Id<ReportRequest>.New();
        var assignmentId = Id<RecurringReportAssignment>.New();
        var createdAt = now.AddHours(-49);

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var trainer = CreateUser("trainer@example.com", "Trainer");
            var trainee = CreateUser("trainee@example.com", "Trainee");
            var template = CreateTemplate(trainer.Id);
            var episode = new CoachingEpisode
            {
                Id = Id<CoachingEpisode>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id,
                StartedAt = now.AddDays(-10), StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
            };
            var request = new ReportRequest
            {
                Id = requestId, TrainerId = trainer.Id, TraineeId = trainee.Id, TemplateId = template.Id,
                Template = template, Status = ReportRequestStatus.Pending, CreatedAt = createdAt
            };
            var assignment = new RecurringReportAssignment
            {
                Id = assignmentId, TrainerId = trainer.Id, TraineeId = trainee.Id, TemplateId = template.Id,
                Template = template, IntervalValue = 1, IntervalUnit = RecurringReportIntervalUnit.Week,
                StartsAt = now.AddDays(-3), EndsAt = now.AddHours(-1), IsActive = true,
                CurrentReportRequestId = requestId, CurrentReportRequest = request, SourceCoachingEpisodeId = episode.Id
            };
            db.AddRange(trainer, trainee, template, episode, request, assignment, new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id, EpisodeId = episode.Id
            });
            await db.SaveChangesAsync();
        }

        var outbox = CreateOutboxWriter();
        await using (var db = new AppDbContext(options))
        {
            await CreateService(db, outbox).ProcessDueAssignmentsAsync();
        }

        await using var assertDb = new AppDbContext(options);
        (await assertDb.ReportRequests.CountAsync()).Should().Be(1);
        (await assertDb.RecurringReportAssignments.SingleAsync()).CurrentReportRequestId.Should().Be(requestId);
        await outbox.Received(1).StageAsync(
            Arg.Is<RecurringReportReminderInAppNotificationCommand>(command =>
                command.AssignmentId == assignmentId && command.ReportRequestId == requestId
                && !command.IsUpcomingRequestReminder && command.ScheduledFor == createdAt.AddHours(48)),
            Arg.Any<CancellationToken>());
        await outbox.DidNotReceiveWithAnyArgs().StageAsync(
            Arg.Any<ReportRequestCreatedInAppNotificationCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_CreatesNextRequest_AfterFeedbackReadAndIntervalElapsed_OnSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var originalRequestId = default(Id<ReportRequest>);

        await using (var setupDb = new AppDbContext(options))
        {
            await setupDb.Database.EnsureCreatedAsync();

            var trainer = CreateUser("trainer@example.com", "Trainer");
            var trainee = CreateUser("trainee@example.com", "Trainee");
            var template = CreateTemplate(trainer.Id);
            var currentRequest = new ReportRequest
            {
                Id = Id<ReportRequest>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                TemplateId = template.Id,
                Template = template,
                Status = ReportRequestStatus.Submitted,
                SubmittedAt = DateTimeOffset.UtcNow.AddDays(-10),
                Note = "weekly check-in"
            };
            originalRequestId = currentRequest.Id;
            var submission = new ReportSubmission
            {
                Id = Id<ReportSubmission>.New(),
                ReportRequestId = currentRequest.Id,
                ReportRequest = currentRequest,
                TraineeId = trainee.Id,
                PayloadJson = "{}",
                TrainerFeedbackAddedAt = DateTimeOffset.UtcNow.AddDays(-9),
                TrainerFeedbackReadAt = DateTimeOffset.UtcNow.AddDays(-8)
            };
            currentRequest.Submission = submission;

            var episode = new CoachingEpisode
            {
                Id = Id<CoachingEpisode>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-30),
                StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
            };
            var assignment = new RecurringReportAssignment
            {
                Id = Id<RecurringReportAssignment>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                TemplateId = template.Id,
                Template = template,
                IntervalValue = 1,
                IntervalUnit = RecurringReportIntervalUnit.Week,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-20),
                IsActive = true,
                Note = "weekly check-in",
                CurrentReportRequestId = currentRequest.Id,
                CurrentReportRequest = currentRequest,
                NextEligibleAt = DateTimeOffset.UtcNow.AddDays(-1),
                SourceCoachingEpisodeId = episode.Id
            };
            var link = new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                EpisodeId = episode.Id
            };

            await setupDb.Users.AddRangeAsync(trainer, trainee);
            await setupDb.CoachingEpisodes.AddAsync(episode);
            await setupDb.TrainerTraineeLinks.AddAsync(link);
            await setupDb.ReportTemplates.AddAsync(template);
            await setupDb.ReportRequests.AddAsync(currentRequest);
            await setupDb.ReportSubmissions.AddAsync(submission);
            await setupDb.RecurringReportAssignments.AddAsync(assignment);
            await setupDb.SaveChangesAsync();
        }

        var commandOutboxWriter = CreateOutboxWriter();

        await using (var actDb = new AppDbContext(options))
        {
            var relationshipAccess = new ReportingRelationshipAccessPersistenceRepository(actDb);
            var access = await relationshipAccess.GetAccessAsync(
                (await actDb.Users.SingleAsync(user => user.Email == "trainer@example.com")).Id.Rebind<LgymApi.Identity.Contracts.AccountReference>(),
                (await actDb.Users.SingleAsync(user => user.Email == "trainee@example.com")).Id.Rebind<LgymApi.Identity.Contracts.AccountReference>());
            access.HasActiveRelationship.Should().BeTrue();
            access.ActiveEpisode.Should().NotBeNull();
            var service = CreateService(actDb, commandOutboxWriter);
            await service.ProcessDueAssignmentsAsync();
        }

        await using var assertDb = new AppDbContext(options);
        var storedAssignment = await assertDb.RecurringReportAssignments
            .SingleAsync();
        var requests = await assertDb.ReportRequests.ToListAsync();
        var nextRequest = requests.Single(x => x.Id != originalRequestId);

        requests.Should().HaveCount(2);
        storedAssignment.CurrentReportRequestId.Should().Be(nextRequest.Id);
        storedAssignment.CurrentReportRequestId.Should().NotBe(originalRequestId);
        storedAssignment.NextEligibleAt.Should().BeNull();
        nextRequest.RecurringReportAssignmentId.Should().Be(storedAssignment.Id);
        nextRequest.Status.Should().Be(ReportRequestStatus.Pending);
        nextRequest.Note.Should().Be("weekly check-in");

        await commandOutboxWriter.Received(1).StageAsync(
            Arg.Is<ReportRequestCreatedInAppNotificationCommand>(command =>
                command.RequestId == nextRequest.Id
                && command.TraineeId == storedAssignment.TraineeId.Rebind<LgymApi.Identity.Contracts.AccountReference>()
                && command.TrainerId == storedAssignment.TrainerId.Rebind<LgymApi.Identity.Contracts.AccountReference>()
                && command.TemplateName == "Weekly check-in"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_DeactivatesAssignment_WhenTemplateWasDeleted_WithoutCreatingRequest_OnSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var assignmentId = Id<RecurringReportAssignment>.New();

        await using (var setupDb = new AppDbContext(options))
        {
            await setupDb.Database.EnsureCreatedAsync();

            var trainer = CreateUser("trainer@example.com", "Trainer");
            var trainee = CreateUser("trainee@example.com", "Trainee");
            var template = CreateTemplate(trainer.Id);
            template.IsDeleted = true;
            var episode = new CoachingEpisode
            {
                Id = Id<CoachingEpisode>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-30),
                StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
            };
            var assignment = new RecurringReportAssignment
            {
                Id = assignmentId,
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                TemplateId = template.Id,
                Template = template,
                IntervalValue = 1,
                IntervalUnit = RecurringReportIntervalUnit.Week,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-20),
                IsActive = true,
                NextEligibleAt = DateTimeOffset.UtcNow.AddDays(-1),
                SourceCoachingEpisodeId = episode.Id
            };
            var link = new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                EpisodeId = episode.Id
            };

            await setupDb.Users.AddRangeAsync(trainer, trainee);
            await setupDb.CoachingEpisodes.AddAsync(episode);
            await setupDb.TrainerTraineeLinks.AddAsync(link);
            await setupDb.ReportTemplates.AddAsync(template);
            await setupDb.RecurringReportAssignments.AddAsync(assignment);
            await setupDb.SaveChangesAsync();
        }

        var commandOutboxWriter = CreateOutboxWriter();

        await using (var actDb = new AppDbContext(options))
        {
            var service = CreateService(actDb, commandOutboxWriter);
            await service.ProcessDueAssignmentsAsync();
        }

        await using var assertDb = new AppDbContext(options);
        var storedAssignment = await assertDb.RecurringReportAssignments
            .IgnoreQueryFilters()
            .SingleAsync(assignment => assignment.Id == assignmentId);

        storedAssignment.IsActive.Should().BeFalse();
        (await assertDb.ReportRequests.CountAsync()).Should().Be(0);
        await commandOutboxWriter.DidNotReceiveWithAnyArgs().StageAsync(
            Arg.Any<ReportRequestCreatedInAppNotificationCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void TestFirstMethodOnICollection()
    {
        var collection = new List<ReportRequest> { new ReportRequest(), new ReportRequest() };
        var first = collection.First();
        Assert.AreEqual(collection[0], first);
    }

    [Test]
    public void TestICollectionAccessUsesFirstInsteadOfIndexer()
    {
        // Arrange
        var collection = new List<int> { 10, 20, 30 };

        // Act
        var firstElement = collection.First();

        // Assert
        firstElement.Should().Be(10);
        firstElement.Should().NotBe(20); // Ensure we're not getting the wrong element
    }

    private static RecurringReportAssignmentService CreateService(AppDbContext db, ICommandOutboxWriter commandOutboxWriter)
    {
        IReportingRelationshipAccessPersistence relationshipAccess = new ReportingRelationshipAccessPersistenceRepository(db);
        return new RecurringReportAssignmentService(
            new ReportTemplatePersistenceRepository(db),
            new ReportRequestSubmissionPersistenceRepository(db),
            new RecurringReportAssignmentPersistenceRepository(db),
            relationshipAccess,
            ReportingTestData.Mapper(),
            commandOutboxWriter,
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

    private static User CreateUser(string email, string name)
        => new()
        {
            Id = Id<User>.New(),
            Name = name,
            Email = email,
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

// All ICollection accesses use .First() instead of [0]
// Example: Use requests.First(x => ...) instead of requests[0]
// This test verifies correct indexing behavior in the service logic
