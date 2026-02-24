using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class ExerciseScoreSeeder : IEntitySeeder
{
    public int Order => 41;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("exercise scores");
        if (seedContext.ExerciseScores.Count > 0)
        {
            SeedOperationConsole.Skip("exercise scores");
            return;
        }

        var trainings = seedContext.Trainings;
        var exercises = seedContext.Exercises;
        if (trainings.Count == 0 || exercises.Count == 0)
        {
            SeedOperationConsole.Skip("exercise scores");
            return;
        }

        var existingPairs = await context.ExerciseScores
            .AsNoTracking()
            .Select(score => new { score.TrainingId, score.ExerciseId })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<(Guid TrainingId, Guid ExerciseId)>(
            existingPairs.Select(pair => (pair.TrainingId, pair.ExerciseId)));

        var scoreIndex = 0;
        var addedAny = false;
        foreach (var training in trainings)
        {
            var exercise = exercises[scoreIndex % exercises.Count];
            if (!existingSet.Add((training.Id, exercise.Id)))
            {
                scoreIndex++;
                continue;
            }
            var score = new ExerciseScore
            {
                Id = Guid.NewGuid(),
                ExerciseId = exercise.Id,
                UserId = training.UserId,
                TrainingId = training.Id,
                Series = 3,
                Reps = 10,
                Weight = 60 + scoreIndex * 2,
                Unit = WeightUnits.Kilograms
            };

            await context.ExerciseScores.AddAsync(score, cancellationToken);
            seedContext.ExerciseScores.Add(score);
            addedAny = true;
            scoreIndex++;
        }

        if (!addedAny)
        {
            SeedOperationConsole.Skip("exercise scores");
            return;
        }

        SeedOperationConsole.Done("exercise scores");
    }
}
