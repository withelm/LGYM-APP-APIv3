using FluentAssertions;
using FluentAssertions.Execution;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Repositories.WorkoutProgress;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseRepositoryTests
{
    [Test]
    public async Task GetTranslationsAsync_WithOnlyEmptyCultures_ReturnsEmptyDictionary()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"exercise-repo-{LgymApi.Domain.ValueObjects.Id<ExerciseRepositoryTests>.New():N}")
            .Options;

        await using var dbContext = new AppDbContext(options);

        var exerciseId = LgymApi.Domain.ValueObjects.Id<Exercise>.New();
        dbContext.Exercises.Add(new Exercise
        {
            Id = exerciseId,
            Name = "Bench press"
        });
        dbContext.ExerciseTranslations.Add(new ExerciseTranslation
        {
            Id = LgymApi.Domain.ValueObjects.Id<ExerciseTranslation>.New(),
            ExerciseId = exerciseId,
            Culture = "en",
            Name = "Bench press"
        });
        await dbContext.SaveChangesAsync();

        var repository = new ExerciseRepository(dbContext);
        var result = await repository.GetTranslationsAsync([exerciseId], ["   ", ""], CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task ScopedFinds_EnforceGlobalOwnedAndUnrestrictedVisibility()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"exercise-scoped-repo-{Id<ExerciseRepositoryTests>.New():N}")
            .Options;
        await using var dbContext = new AppDbContext(options);
        var actorId = Id<AccountReference>.New();
        var foreignOwnerId = Id<AccountReference>.New();
        var globalExercise = Exercise(Id<Exercise>.New(), null, "Global");
        var ownedExercise = Exercise(Id<Exercise>.New(), actorId.Rebind<User>(), "Owned");
        var foreignExercise = Exercise(Id<Exercise>.New(), foreignOwnerId.Rebind<User>(), "Foreign");
        dbContext.Exercises.AddRange(globalExercise, ownedExercise, foreignExercise);
        await dbContext.SaveChangesAsync();
        var repository = new WorkoutExercisePersistenceRepository(dbContext);

        var visibleGlobal = await repository.FindVisibleToAccountAsync(globalExercise.Id, actorId);
        var visibleOwned = await repository.FindVisibleToAccountAsync(ownedExercise.Id, actorId);
        var visibleForeign = await repository.FindVisibleToAccountAsync(foreignExercise.Id, actorId);
        var owned = await repository.FindOwnedByAccountAsync(ownedExercise.Id, actorId);
        var foreignOwned = await repository.FindOwnedByAccountAsync(foreignExercise.Id, actorId);
        var unrestrictedForeign = await repository.FindUnrestrictedByIdAsync(foreignExercise.Id);
        var global = await repository.FindGlobalByIdAsync(globalExercise.Id);
        var foreignGlobal = await repository.FindGlobalByIdAsync(foreignExercise.Id);

        using (new AssertionScope())
        {
            visibleGlobal.Should().NotBeNull();
            visibleGlobal!.OwnerId.Should().BeNull();
            visibleOwned.Should().NotBeNull();
            visibleOwned!.OwnerId.Should().Be(actorId);
            visibleForeign.Should().BeNull();
            owned.Should().NotBeNull();
            owned!.OwnerId.Should().Be(actorId);
            foreignOwned.Should().BeNull();
            unrestrictedForeign.Should().NotBeNull();
            unrestrictedForeign!.OwnerId.Should().Be(foreignOwnerId);
            global.Should().NotBeNull();
            global!.OwnerId.Should().BeNull();
            foreignGlobal.Should().BeNull();
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("https://old.test/stale.gif")]
    public async Task MetadataReadBeforeExplicitImageChange_DoesNotWriteOrRevertImage(string? staleImage)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var metadata = new AppDbContext(options);
        await metadata.Database.EnsureCreatedAsync();
        var exercise = Exercise(Id<Exercise>.New(), null, "Before"); exercise.Image = "https://old.test/stale.gif";
        metadata.Exercises.Add(exercise); await metadata.SaveChangesAsync();
        var stale = await new WorkoutExercisePersistenceRepository(metadata).FindGlobalByIdAsync(exercise.Id);
        await using (var imageContext = new AppDbContext(options))
        {
            await new WorkoutExercisePersistenceRepository(imageContext).UpdateAsync(new(stale!.Id, null, stale.Name, stale.BodyPart, stale.EloFormula, stale.Description, "https://new.test/current.gif", false, true));
            await imageContext.SaveChangesAsync();
        }
        await new WorkoutExercisePersistenceRepository(metadata).UpdateAsync(new(stale!.Id, null, "Metadata saved later", stale.BodyPart, stale.EloFormula, stale.Description, staleImage, false));
        metadata.ChangeTracker.DetectChanges();
        metadata.Entry(exercise).Property(x => x.Image).IsModified.Should().BeFalse();
        await metadata.SaveChangesAsync();
        await using var read = new AppDbContext(options);
        (await read.Exercises.SingleAsync(x => x.Id == exercise.Id)).Image.Should().Be("https://new.test/current.gif");
        (await read.Exercises.SingleAsync(x => x.Id == exercise.Id)).Name.Should().Be("Metadata saved later");
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task ImageReferences_IncludePersonalAndSoftDeletedRecords(bool personal, bool deleted)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"image-references-{Id<ExerciseRepositoryTests>.New():N}").Options;
        await using var db = new AppDbContext(options);
        var record = Exercise(Id<Exercise>.New(), personal ? Id<User>.New() : null, "Referenced");
        record.Image = "https://media.test/shared.gif";
        record.IsDeleted = deleted;
        db.Exercises.Add(record);
        await db.SaveChangesAsync();
        var repository = new WorkoutExercisePersistenceRepository(db);
        (await repository.IsImageReferencedIncludingDeletedAsync(record.Image)).Should().BeTrue();
        (await repository.IsImageReferencedIncludingDeletedAsync("https://media.test/unused.gif")).Should().BeFalse();
    }

    private static Exercise Exercise(Id<Exercise> id, Id<User>? ownerId, string name) => new()
    {
        Id = id,
        UserId = ownerId,
        Name = name,
        BodyPart = BodyParts.Chest,
        EloFormula = ExerciseEloFormula.Standard
    };
}
