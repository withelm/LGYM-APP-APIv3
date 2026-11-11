using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportRequestCancellationConcurrencyTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task CompetingExpirationAndCancellation_ConflictsAndCancellationWinsAfterRetry(bool cancellationFirst)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var episodeId = Id<CoachingEpisode>.New();
        var requestId = Id<ReportRequest>.New();
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var trainer = new User { Id = Id<User>.New(), Name = "trainer", Email = "trainer@example.com", ProfileRank = "Rookie" };
            var trainee = new User { Id = Id<User>.New(), Name = "trainee", Email = "trainee@example.com", ProfileRank = "Rookie" };
            var template = new ReportTemplate { Id = Id<ReportTemplate>.New(), TrainerId = trainer.Id, Name = "Report" };
            setup.AddRange(trainer, trainee, template, new ReportRequest
            {
                Id = requestId, TrainerId = trainer.Id, TraineeId = trainee.Id,
                TemplateId = template.Id, SourceCoachingEpisodeId = episodeId, Status = ReportRequestStatus.Pending
            });
            await setup.SaveChangesAsync();
        }

        await using var expiration = new AppDbContext(options);
        await using var cancellation = new AppDbContext(options);
        await new ReportRequestSubmissionPersistenceRepository(expiration).SetRequestExpiredAsync(requestId);
        await new ReportRequestSubmissionPersistenceRepository(cancellation).CancelPendingForSourceEpisodeAsync(episodeId);

        var winner = cancellationFirst ? cancellation : expiration;
        var loser = cancellationFirst ? expiration : cancellation;
        await winner.SaveChangesAsync();
        var saveLoser = async () => await loser.SaveChangesAsync();
        await saveLoser.Should().ThrowAsync<DbUpdateConcurrencyException>();

        if (!cancellationFirst)
        {
            await using var retry = new AppDbContext(options);
            await new ReportRequestSubmissionPersistenceRepository(retry).CancelPendingForSourceEpisodeAsync(episodeId);
            await retry.SaveChangesAsync();
        }

        await using var verification = new AppDbContext(options);
        (await verification.ReportRequests.AsNoTracking().SingleAsync(request => request.Id == requestId)).Status
            .Should().Be(ReportRequestStatus.Cancelled);
    }
}
