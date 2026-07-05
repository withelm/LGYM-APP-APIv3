using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class Issue313EfLinqOrderingTests
{
    [Test]
    public async Task PlanDayExerciseRepository_SortsByPlanDayOrderAndTypedIdTieBreak()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConnection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        static byte[] Bytes(byte lastByte)
        {
            var bytes = new byte[16];
            bytes[15] = lastByte;
            return bytes;
        }

        var userId = Id<User>.FromBytes(Bytes(1));
        var planId = Id<Plan>.FromBytes(Bytes(2));
        var planDayId = Id<PlanDay>.FromBytes(Bytes(3));
        var exerciseAId = Id<Exercise>.FromBytes(Bytes(11));
        var exerciseBId = Id<Exercise>.FromBytes(Bytes(12));
        var exerciseCId = Id<Exercise>.FromBytes(Bytes(13));

        await dbContext.AddRangeAsync(
            new User { Id = userId, Name = "user", Email = "user@example.com", ProfileRank = "Junior 1" },
            new Plan { Id = planId, UserId = userId, Name = "Plan", IsActive = true },
            new PlanDay { Id = planDayId, PlanId = planId, Name = "Plan Day" },
            new Exercise { Id = exerciseAId, Name = "Exercise A", BodyPart = BodyParts.Chest },
            new Exercise { Id = exerciseBId, Name = "Exercise B", BodyPart = BodyParts.Back },
            new Exercise { Id = exerciseCId, Name = "Exercise C", BodyPart = BodyParts.Quads },
            new PlanDayExercise { Id = Id<PlanDayExercise>.FromBytes(Bytes(21)), PlanDayId = planDayId, ExerciseId = exerciseCId, Order = 0, Series = 3, Reps = "8" },
            new PlanDayExercise { Id = Id<PlanDayExercise>.FromBytes(Bytes(22)), PlanDayId = planDayId, ExerciseId = exerciseAId, Order = 0, Series = 4, Reps = "10" },
            new PlanDayExercise { Id = Id<PlanDayExercise>.FromBytes(Bytes(23)), PlanDayId = planDayId, ExerciseId = exerciseBId, Order = 0, Series = 5, Reps = "12" });
        await dbContext.SaveChangesAsync();

        var repository = new PlanDayExerciseRepository(dbContext);

        var result = await repository.GetByPlanDayIdAsync(planDayId);

        result.Select(x => x.ExerciseId).Should().Equal(
            exerciseAId,
            exerciseBId,
            exerciseCId);
    }

    [Test]
    public async Task TrainingExerciseScoreRepository_SortsByTrainingOrderAndTypedIdTieBreak()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConnection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        static byte[] Bytes(byte lastByte)
        {
            var bytes = new byte[16];
            bytes[15] = lastByte;
            return bytes;
        }

        var userId = Id<User>.FromBytes(Bytes(1));
        var planId = Id<Plan>.FromBytes(Bytes(2));
        var planDayId = Id<PlanDay>.FromBytes(Bytes(3));
        var gymId = Id<Gym>.FromBytes(Bytes(4));
        var exerciseId = Id<Exercise>.FromBytes(Bytes(5));
        var trainingAId = Id<Training>.FromBytes(Bytes(6));
        var trainingBId = Id<Training>.FromBytes(Bytes(7));

        var scoreA1Id = Id<ExerciseScore>.FromBytes(Bytes(11));
        var scoreA2Id = Id<ExerciseScore>.FromBytes(Bytes(12));
        var scoreA3Id = Id<ExerciseScore>.FromBytes(Bytes(13));
        var scoreB1Id = Id<ExerciseScore>.FromBytes(Bytes(14));

        var trainingScoreA1Id = Id<TrainingExerciseScore>.FromBytes(Bytes(21));
        var trainingScoreA2Id = Id<TrainingExerciseScore>.FromBytes(Bytes(22));
        var trainingScoreA3Id = Id<TrainingExerciseScore>.FromBytes(Bytes(23));
        var trainingScoreB1Id = Id<TrainingExerciseScore>.FromBytes(Bytes(24));

        await dbContext.AddRangeAsync(
            new User { Id = userId, Name = "user", Email = "user@example.com", ProfileRank = "Junior 1" },
            new Plan { Id = planId, UserId = userId, Name = "Plan", IsActive = true },
            new PlanDay { Id = planDayId, PlanId = planId, Name = "Plan Day" },
            new Gym { Id = gymId, UserId = userId, Name = "Gym" },
            new Exercise { Id = exerciseId, Name = "Bench Press", BodyPart = BodyParts.Chest },
            new Training { Id = trainingAId, UserId = userId, TypePlanDayId = planDayId, GymId = gymId },
            new Training { Id = trainingBId, UserId = userId, TypePlanDayId = planDayId, GymId = gymId },
            new ExerciseScore
            {
                Id = scoreA1Id,
                ExerciseId = exerciseId,
                UserId = userId,
                Reps = 10,
                Series = 1,
                Weight = new Weight(100, WeightUnits.Kilograms),
                TrainingId = trainingAId,
                Order = 0
            },
            new ExerciseScore
            {
                Id = scoreA2Id,
                ExerciseId = exerciseId,
                UserId = userId,
                Reps = 8,
                Series = 1,
                Weight = new Weight(80, WeightUnits.Kilograms),
                TrainingId = trainingAId,
                Order = 0
            },
            new ExerciseScore
            {
                Id = scoreA3Id,
                ExerciseId = exerciseId,
                UserId = userId,
                Reps = 6,
                Series = 1,
                Weight = new Weight(120, WeightUnits.Kilograms),
                TrainingId = trainingAId,
                Order = 1
            },
            new ExerciseScore
            {
                Id = scoreB1Id,
                ExerciseId = exerciseId,
                UserId = userId,
                Reps = 5,
                Series = 1,
                Weight = new Weight(130, WeightUnits.Kilograms),
                TrainingId = trainingBId,
                Order = 0
            },
            new TrainingExerciseScore { Id = trainingScoreB1Id, TrainingId = trainingBId, ExerciseScoreId = scoreB1Id, Order = 0 },
            new TrainingExerciseScore { Id = trainingScoreA3Id, TrainingId = trainingAId, ExerciseScoreId = scoreA3Id, Order = 1 },
            new TrainingExerciseScore { Id = trainingScoreA2Id, TrainingId = trainingAId, ExerciseScoreId = scoreA2Id, Order = 0 },
            new TrainingExerciseScore { Id = trainingScoreA1Id, TrainingId = trainingAId, ExerciseScoreId = scoreA1Id, Order = 0 });
        await dbContext.SaveChangesAsync();

        var repository = new TrainingExerciseScoreRepository(dbContext);

        var result = await repository.GetByTrainingIdsAsync(new List<Id<Training>> { trainingBId, trainingAId });

        result.Select(score => (score.TrainingId, score.Order, score.Id)).Should().Equal(
            (trainingAId, 0, trainingScoreA1Id),
            (trainingAId, 0, trainingScoreA2Id),
            (trainingAId, 1, trainingScoreA3Id),
            (trainingBId, 0, trainingScoreB1Id));
    }
}
