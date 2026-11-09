using FluentAssertions;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
internal sealed class PostgreSqlCoachingTraineeNoteConcurrencyTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task UpdateNote_StaleDetachedUpdateAfterHide_DoesNotRestoreSharingOrPersistLoserHistory()
    {
        var trainer = await SeedUserAsync("note-concurrency-trainer", "note-concurrency-trainer@example.test");
        var trainee = await SeedUserAsync("note-concurrency-trainee", "note-concurrency-trainee@example.test");
        var noteId = Id<TraineeNote>.New();
        var hidingHistoryId = Id<TraineeNoteHistory>.New();
        var staleHistoryId = Id<TraineeNoteHistory>.New();

        await using (var seedScope = Factory.Services.CreateAsyncScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(new TraineeNote
            {
                Id = noteId,
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                Title = "Originally shared",
                Content = "Initial shared content",
                VisibleToTrainee = true,
                LastUpdatedByUserId = trainer.Id,
                LastUpdatedAt = DateTimeOffset.UtcNow
            });
            await database.SaveChangesAsync();
        }

        await using var hidingScope = Factory.Services.CreateAsyncScope();
        await using var staleScope = Factory.Services.CreateAsyncScope();
        var hidingNotes = hidingScope.ServiceProvider.GetRequiredService<ICoachingTraineeNotePersistence>();
        var hidingUnitOfWork = hidingScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var staleNotes = staleScope.ServiceProvider.GetRequiredService<ICoachingTraineeNotePersistence>();
        var staleUnitOfWork = staleScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var hidingSnapshot = await hidingNotes.FindNoteByIdAsync(noteId);
        var staleSnapshot = await staleNotes.FindNoteByIdAsync(noteId);

        hidingSnapshot.Should().NotBeNull();
        staleSnapshot.Should().NotBeNull();
        hidingSnapshot!.UpdatedAt.Should().Be(staleSnapshot!.UpdatedAt);
        var expectedUpdatedAt = hidingSnapshot.UpdatedAt;
        var hiddenAt = DateTimeOffset.UtcNow;

        await hidingNotes.UpdateNoteAsync(new CoachingTraineeNoteWriteModel(
            noteId,
            trainer.Id,
            trainee.Id,
            "Hidden revision",
            "Hidden content",
            false,
            false,
            trainer.Id,
            hiddenAt,
            ExpectedUpdatedAt: expectedUpdatedAt));
        await hidingNotes.AddHistoryEntryAsync(new CoachingTraineeNoteHistoryWriteModel(
            hidingHistoryId,
            noteId,
            trainer.Id,
            hiddenAt,
            "Initial shared content",
            "Hidden content",
            true,
            false,
            "Updated"));
        await hidingUnitOfWork.SaveChangesAsync();

        await staleNotes.UpdateNoteAsync(new CoachingTraineeNoteWriteModel(
            noteId,
            trainer.Id,
            trainee.Id,
            "Stale restored revision",
            "Stale restored content",
            true,
            false,
            trainer.Id,
            hiddenAt.AddMinutes(1),
            ExpectedUpdatedAt: expectedUpdatedAt));
        await staleNotes.AddHistoryEntryAsync(new CoachingTraineeNoteHistoryWriteModel(
            staleHistoryId,
            noteId,
            trainer.Id,
            hiddenAt.AddMinutes(1),
            "Initial shared content",
            "Stale restored content",
            true,
            true,
            "Updated"));

        var staleSave = () => staleUnitOfWork.SaveChangesAsync();
        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verificationNotes = verificationScope.ServiceProvider.GetRequiredService<ICoachingTraineeNotePersistence>();
        var persistedNote = await verificationDatabase.TraineeNotes.SingleAsync(note => note.Id == noteId);
        var persistedHistory = await verificationDatabase.TraineeNoteHistories
            .Where(entry => entry.TraineeNoteId == noteId)
            .OrderBy(entry => entry.ChangedAt)
            .ToListAsync();
        var visibleHistory = await verificationNotes.GetVisibleHistoryAsync(trainee.Id, noteId);

        persistedNote.VisibleToTrainee.Should().BeFalse();
        persistedNote.Content.Should().Be("Hidden content");
        persistedHistory.Should().ContainSingle(entry => entry.Id == hidingHistoryId && !entry.NewVisibleToTrainee);
        persistedHistory.Should().NotContain(entry => entry.Id == staleHistoryId || entry.NewContent == "Stale restored content");
        visibleHistory.Should().BeNull();
    }
}
