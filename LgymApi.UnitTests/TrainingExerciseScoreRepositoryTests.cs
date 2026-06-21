using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingExerciseScoreRepositoryTests
{
    [Test]
    public async Task GetByTrainingIdsAsync_ReturnsScoresOrderedByTrainingIdOrderAndId()
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
        var trainingAId = Id<Training>.FromBytes(Bytes(2));
        var trainingBId = Id<Training>.FromBytes(Bytes(3));
        var planId = Id<Plan>.FromBytes(Bytes(4));
        var planDayId = Id<PlanDay>.FromBytes(Bytes(5));
        var gymId = Id<Gym>.FromBytes(Bytes(6));
        var exerciseId = Id<Exercise>.FromBytes(Bytes(7));

        var user = new User
        {
            Id = userId,
            Name = "user",
            Email = "user@example.com",
            ProfileRank = "Junior 1"
        };

        var plan = new Plan
        {
            Id = planId,
            UserId = userId,
            Name = "Plan",
            IsActive = true
        };

        var planDay = new PlanDay
        {
            Id = planDayId,
            PlanId = planId,
            Name = "Plan Day"
        };

        var gym = new Gym
        {
            Id = gymId,
            UserId = userId,
            Name = "Gym"
        };

        var exercise = new Exercise
        {
            Id = exerciseId,
            Name = "Bench Press",
            BodyPart = BodyParts.Chest
        };

        var trainingA = new Training
        {
            Id = trainingAId,
            UserId = userId,
            TypePlanDayId = planDayId,
            GymId = gymId
        };

        var trainingB = new Training
        {
            Id = trainingBId,
            UserId = userId,
            TypePlanDayId = planDayId,
            GymId = gymId
        };

        var scoreA2 = new ExerciseScore
        {
            Id = Id<ExerciseScore>.FromBytes(Bytes(12)),
            ExerciseId = exerciseId,
            UserId = userId,
            Reps = 8,
            Series = 1,
            Weight = new Weight(80, WeightUnits.Kilograms),
            TrainingId = trainingAId,
            Order = 0
        };

        var scoreA1 = new ExerciseScore
        {
            Id = Id<ExerciseScore>.FromBytes(Bytes(11)),
            ExerciseId = exerciseId,
            UserId = userId,
            Reps = 10,
            Series = 1,
            Weight = new Weight(100, WeightUnits.Kilograms),
            TrainingId = trainingAId,
            Order = 0
        };

        var scoreA3 = new ExerciseScore
        {
            Id = Id<ExerciseScore>.FromBytes(Bytes(13)),
            ExerciseId = exerciseId,
            UserId = userId,
            Reps = 6,
            Series = 1,
            Weight = new Weight(120, WeightUnits.Kilograms),
            TrainingId = trainingAId,
            Order = 1
        };

        var scoreB1 = new ExerciseScore
        {
            Id = Id<ExerciseScore>.FromBytes(Bytes(14)),
            ExerciseId = exerciseId,
            UserId = userId,
            Reps = 5,
            Series = 1,
            Weight = new Weight(130, WeightUnits.Kilograms),
            TrainingId = trainingBId,
            Order = 0
        };

        var trainingScoreB1Id = Id<TrainingExerciseScore>.FromBytes(Bytes(24));
        var trainingScoreA3Id = Id<TrainingExerciseScore>.FromBytes(Bytes(23));
        var trainingScoreA2Id = Id<TrainingExerciseScore>.FromBytes(Bytes(22));
        var trainingScoreA1Id = Id<TrainingExerciseScore>.FromBytes(Bytes(21));

        var trainingExerciseScores = new[]
        {
            new TrainingExerciseScore
            {
                Id = trainingScoreB1Id,
                TrainingId = trainingBId,
                ExerciseScoreId = scoreB1.Id,
                Order = 0
            },
            new TrainingExerciseScore
            {
                Id = trainingScoreA3Id,
                TrainingId = trainingAId,
                ExerciseScoreId = scoreA3.Id,
                Order = 1
            },
            new TrainingExerciseScore
            {
                Id = trainingScoreA2Id,
                TrainingId = trainingAId,
                ExerciseScoreId = scoreA2.Id,
                Order = 0
            },
            new TrainingExerciseScore
            {
                Id = trainingScoreA1Id,
                TrainingId = trainingAId,
                ExerciseScoreId = scoreA1.Id,
                Order = 0
            }
        };

        await dbContext.AddRangeAsync(user, plan, planDay, gym, exercise, trainingA, trainingB, scoreA1, scoreA2, scoreA3, scoreB1);
        await dbContext.AddRangeAsync(trainingExerciseScores);
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
