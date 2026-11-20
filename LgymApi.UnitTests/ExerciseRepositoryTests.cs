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

        Assert.That(result, Is.Empty);
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(visibleGlobal, Is.Not.Null);
            Assert.That(visibleGlobal!.OwnerId, Is.Null);
            Assert.That(visibleOwned, Is.Not.Null);
            Assert.That(visibleOwned!.OwnerId, Is.EqualTo(actorId));
            Assert.That(visibleForeign, Is.Null);
            Assert.That(owned, Is.Not.Null);
            Assert.That(owned!.OwnerId, Is.EqualTo(actorId));
            Assert.That(foreignOwned, Is.Null);
            Assert.That(unrestrictedForeign, Is.Not.Null);
            Assert.That(unrestrictedForeign!.OwnerId, Is.EqualTo(foreignOwnerId));
            Assert.That(global, Is.Not.Null);
            Assert.That(global!.OwnerId, Is.Null);
            Assert.That(foreignGlobal, Is.Null);
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
        Assert.That(metadata.Entry(exercise).Property(x => x.Image).IsModified, Is.False);
        await metadata.SaveChangesAsync();
        await using var read = new AppDbContext(options);
        Assert.That((await read.Exercises.SingleAsync(x => x.Id == exercise.Id)).Image, Is.EqualTo("https://new.test/current.gif"));
        Assert.That((await read.Exercises.SingleAsync(x => x.Id == exercise.Id)).Name, Is.EqualTo("Metadata saved later"));
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
