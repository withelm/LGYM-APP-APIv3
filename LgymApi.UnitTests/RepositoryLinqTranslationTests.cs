using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RepositoryLinqTranslationTests
{
    [Test]
    public async Task PlanDayExerciseRepository_AddRangeAsync_DoesNotThrow_LinqTranslationException()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConnection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var repo = new PlanDayExerciseRepository(dbContext);

        var planDayId = Id<PlanDay>.New();
        var exercises = new[]
        {
            new PlanDayExercise
            {
                Id = Id<PlanDayExercise>.New(),
                PlanDayId = planDayId,
                ExerciseId = Id<Exercise>.New(),
                Order = 10,
                Series = 3,
                Reps = "10"
            }
        };

        Func<Task> act = async () => await repo.AddRangeAsync(exercises);
        await act.Should().NotThrowAsync();

        var loaded = await repo.GetByPlanDayIdsAsync(new List<Id<PlanDay>> { planDayId });
        loaded.Should().BeEmpty();
    }

    [Test]
    public async Task TrainingExerciseScoreRepository_GetByTrainingIdsAsync_DoesNotThrow_LinqTranslationException()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConnection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var repo = new TrainingExerciseScoreRepository(dbContext);
        var trainingId = Id<Training>.New();

        var loaded = await repo.GetByTrainingIdsAsync(new List<Id<Training>> { trainingId });
        loaded.Should().BeEmpty();
    }
}

