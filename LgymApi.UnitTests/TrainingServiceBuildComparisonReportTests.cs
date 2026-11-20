using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Application.WorkoutProgress.TrainingExecution;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingServiceBuildComparisonReportTests
{
    [Test]
    public void Build_ShouldPreserveExerciseOrderAndPreviousScore()
    {
        var exerciseId = Id<Exercise>.New();
        var current = new[] { new TrainingExerciseInput { ExerciseId = exerciseId, Series = 1, Reps = 8, Weight = 80, Unit = WeightUnits.Kilograms } };
        var previous = new Dictionary<string, WorkoutExerciseScorePersistenceModel>
        {
            [$"{exerciseId}-1"] = Score(exerciseId, 5, 70)
        };

        var result = TrainingComparisonReportBuilder.Build(current, previous, new Dictionary<Id<Exercise>, string> { [exerciseId] = "Bench" });

        Assert.That(result, Has.Exactly(1).Items);
        Assert.That(result[0].ExerciseName, Is.EqualTo("Bench"));
        Assert.That(result[0].SeriesComparisons[0].PreviousResult!.Weight, Is.EqualTo(70));
        Assert.That(result[0].SeriesComparisons[0].CurrentResult.Weight, Is.EqualTo(80));
    }

    [Test]
    public void Build_ShouldSkipEmptyExerciseIds()
    {
        var result = TrainingComparisonReportBuilder.Build(
            [new TrainingExerciseInput { ExerciseId = Id<Exercise>.Empty, Series = 1, Unit = WeightUnits.Kilograms }],
            [],
            []);
        Assert.That(result, Is.Empty);
    }

    private static WorkoutExerciseScorePersistenceModel Score(Id<Exercise> exerciseId, double reps, double weight)
        => new(Id<ExerciseScore>.New(), exerciseId, Id<AccountReference>.New(), reps, 1, weight, WeightUnits.Kilograms, Id<Training>.New(), 0, default, null, null);
}
