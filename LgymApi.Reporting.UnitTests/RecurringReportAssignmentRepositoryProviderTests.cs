using FluentAssertions;
using System.Data.Common;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RecurringReportAssignmentRepositoryProviderTests
{
    [Test]
    public async Task BulkFeedbackFacts_MatchSingleReadsWithConstantSqlCommandCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var counter = new CommandCounter();
        await using var database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(counter).Options);
        await database.Database.EnsureCreatedAsync();
        for (var i = 0; i < 4; i++)
        {
            await SeedAssignmentAsync(database);
        }
        var rows = await database.RecurringReportAssignments.AsNoTracking().ToListAsync();
        foreach (var row in rows)
        {
            var episode = new CoachingEpisode { Id = Id<CoachingEpisode>.New(), TrainerId = row.TrainerId, TraineeId = row.TraineeId, AuthorizationRevision = 7 };
            database.Add(episode);
            database.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = row.TrainerId, TraineeId = row.TraineeId, EpisodeId = episode.Id });
        }
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var relationships = new ReportingRelationshipAccessPersistenceRepository(database);
        var assignments = new RecurringReportAssignmentPersistenceRepository(database);
        var pairs = rows.Select(row => (row.TrainerId.Rebind<AccountReference>(), row.TraineeId.Rebind<AccountReference>())).ToArray();
        var requestIds = rows.Select(row => row.CurrentReportRequestId!.Value).ToArray();
        var expectedAccess = new List<ReportingRelationshipAccessFact>();
        var expectedAssignments = new List<RecurringReportAssignmentPersistenceModel?>();
        for (var i = 0; i < rows.Count; i++)
        {
            expectedAccess.Add(await relationships.GetAccessAsync(pairs[i].Item1, pairs[i].Item2));
            expectedAssignments.Add(await assignments.FindByCurrentRequestAsync(requestIds[i]));
        }

        counter.Count = 0;
        await relationships.GetActiveAccessForPairsAsync(pairs.Take(1).ToArray());
        await assignments.ListByCurrentRequestsAsync(requestIds.Take(1).ToArray());
        var singleCount = counter.Count;
        counter.Count = 0;
        var bulkAccess = await relationships.GetActiveAccessForPairsAsync(pairs);
        var bulkAssignments = await assignments.ListByCurrentRequestsAsync(requestIds);

        counter.Count.Should().Be(singleCount);
        singleCount.Should().BeGreaterThan(0);
        for (var i = 0; i < rows.Count; i++)
        {
            bulkAccess[pairs[i]].ActiveEpisode.Should().Be(expectedAccess[i].ActiveEpisode);
            bulkAssignments.Single(item => item.CurrentReportRequestId == requestIds[i]).Should().BeEquivalentTo(expectedAssignments[i]);
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Test]
    public async Task SqliteWithoutTransaction_RequiresAnActiveTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = CreateSqliteDatabase(connection);
        var assignmentId = await SeedAssignmentAsync(database);
        var repository = new RecurringReportAssignmentPersistenceRepository(database);

        var action = () => repository.FindByIdForUpdateAsync(assignmentId);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SqliteWithTransaction_ReturnsCompleteNonDeletedGraph()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = CreateSqliteDatabase(connection);
        var assignmentId = await SeedAssignmentAsync(database);
        var repository = new RecurringReportAssignmentPersistenceRepository(database);

        await using var transaction = await database.Database.BeginTransactionAsync();
        var result = await repository.FindByIdForUpdateAsync(assignmentId);

        AssertCompleteGraph(result);
    }

    [Test]
    public async Task InMemoryWithoutTransaction_UsesTheNoOpTransactionPath()
    {
        await using var database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"recurring-lock-{Id<RecurringReportAssignmentRepositoryProviderTests>.New():N}")
            .Options);
        var assignmentId = await SeedAssignmentAsync(database);
        var repository = new RecurringReportAssignmentPersistenceRepository(database);

        var result = await repository.FindByIdForUpdateAsync(assignmentId);

        AssertCompleteGraph(result);
    }

    private static AppDbContext CreateSqliteDatabase(SqliteConnection connection)
    {
        var database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);
        database.Database.EnsureCreated();
        return database;
    }

    private static async Task<Id<RecurringReportAssignment>> SeedAssignmentAsync(AppDbContext database)
    {
        var trainer = new User
        {
            Id = Id<User>.New(),
            Name = $"Trainer-{Id<User>.New():N}",
            Email = $"trainer-{Id<User>.New():N}@example.com",
            ProfileRank = "Rookie"
        };
        var trainee = new User
        {
            Id = Id<User>.New(),
            Name = $"Trainee-{Id<User>.New():N}",
            Email = $"trainee-{Id<User>.New():N}@example.com",
            ProfileRank = "Rookie"
        };
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Weekly",
            Fields =
            [
                new ReportTemplateField { Id = Id<ReportTemplateField>.New(), Key = "summary", Order = 2 },
                new ReportTemplateField { Id = Id<ReportTemplateField>.New(), Key = "mood", Order = 1 }
            ]
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted
        };
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = request.Id,
            ReportRequest = request,
            TraineeId = trainee.Id,
            PayloadJson = "{}",
            TrainerFeedbackAddedAt = DateTimeOffset.UtcNow.AddDays(-2),
            TrainerFeedbackReadAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        request.Submission = submission;
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-7),
            IsActive = true,
            CurrentReportRequestId = request.Id,
            CurrentReportRequest = request
        };
        database.AddRange(trainer, trainee, template, request, submission, assignment);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        return assignment.Id;
    }

    private static void AssertCompleteGraph(RecurringReportAssignmentPersistenceModel? result)
    {
        result.Should().NotBeNull();
        result!.IsDeleted.Should().BeFalse();
        result.Template.Fields.Select(field => field.Order).Should().BeInAscendingOrder();
        result.CurrentReportRequest.Should().NotBeNull();
        result.CurrentReportRequest!.Template.Fields.Select(field => field.Order).Should().BeInAscendingOrder();
        result.CurrentReportRequest.Submission.Should().NotBeNull();
    }
}
