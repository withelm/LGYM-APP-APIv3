using LgymApi.Application.Features.Training;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingServiceBuildComparisonReportTests
{
    private TrainingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new TrainingService(new NullTrainingServiceDependencies());
    }

    [Test]
    public void BuildComparisonReport_ReturnsExercisesInCurrentExercisesOrder()
    {
        // Arrange
        var exerciseA = Id<Exercise>.New();
        var exerciseB = Id<Exercise>.New();
        var exerciseC = Id<Exercise>.New();

        // Input order: C, A, B
        var currentExercises = new List<TrainingExerciseInput>
        {
            new() { ExerciseId = exerciseC, Series = 1, Reps = 10, Weight = 60, Unit = WeightUnits.Kilograms },
            new() { ExerciseId = exerciseA, Series = 1, Reps = 8, Weight = 80, Unit = WeightUnits.Kilograms },
            new() { ExerciseId = exerciseB, Series = 1, Reps = 6, Weight = 100, Unit = WeightUnits.Kilograms },
        };

        var previousScores = new Dictionary<string, ExerciseScore>();

        var exerciseDetails = new Dictionary<Id<Exercise>, string>
        {
            [exerciseA] = "Bench Press",
            [exerciseB] = "Squat",
            [exerciseC] = "Deadlift",
        };

        // Act
        var result = _service.BuildComparisonReport(currentExercises, previousScores, exerciseDetails);

        // Assert
         Assert.Multiple(() =>
         {
             Assert.That(result, Has.Count.EqualTo(3));
             Assert.That(result[0].ExerciseId, Is.EqualTo(exerciseC));
             Assert.That(result[1].ExerciseId, Is.EqualTo(exerciseA));
             Assert.That(result[2].ExerciseId, Is.EqualTo(exerciseB));
         });
    }

    [Test]
    public void BuildComparisonReport_WithMultipleSeriesPerExercise_PreservesExerciseOrder()
    {
        // Arrange
        var exerciseA = Id<Exercise>.New();
        var exerciseB = Id<Exercise>.New();

        // B appears first (series 1, 2), then A (series 1, 2)
        var currentExercises = new List<TrainingExerciseInput>
        {
            new() { ExerciseId = exerciseB, Series = 1, Reps = 5, Weight = 120, Unit = WeightUnits.Kilograms },
            new() { ExerciseId = exerciseB, Series = 2, Reps = 5, Weight = 120, Unit = WeightUnits.Kilograms },
            new() { ExerciseId = exerciseA, Series = 1, Reps = 10, Weight = 60, Unit = WeightUnits.Kilograms },
            new() { ExerciseId = exerciseA, Series = 2, Reps = 8, Weight = 65, Unit = WeightUnits.Kilograms },
        };

        var previousScores = new Dictionary<string, ExerciseScore>();

          var exerciseDetails = new Dictionary<Id<Exercise>, string>
          {
              [exerciseA] = "Bench Press",
              [exerciseB] = "Squat",
          };

        // Act
        var result = _service.BuildComparisonReport(currentExercises, previousScores, exerciseDetails);

         // Assert
         Assert.Multiple(() =>
         {
             Assert.That(result, Has.Count.EqualTo(2));
             Assert.That(result[0].ExerciseId, Is.EqualTo(exerciseB));
             Assert.That(result[0].SeriesComparisons, Has.Count.EqualTo(2));
             Assert.That(result[1].ExerciseId, Is.EqualTo(exerciseA));
             Assert.That(result[1].SeriesComparisons, Has.Count.EqualTo(2));
         });
    }

    [Test]
    public void BuildComparisonReport_WithPreviousScores_IncludesPreviousResult()
    {
        // Arrange
        var exerciseId = Id<Exercise>.New();

        var currentExercises = new List<TrainingExerciseInput>
        {
            new() { ExerciseId = exerciseId, Series = 1, Reps = 10, Weight = 80, Unit = WeightUnits.Kilograms },
        };

         var previousScores = new Dictionary<string, ExerciseScore>
         {
             [$"{exerciseId}-1"] = new ExerciseScore
             {
                 Id = Id<ExerciseScore>.New(),
                 ExerciseId = (Id<Exercise>)exerciseId,
                 Reps = 8,
                 Weight = new Weight(75, WeightUnits.Kilograms),
                 Series = 1,
             },
         };

         var exerciseDetails = new Dictionary<Id<Exercise>, string>
         {
             [exerciseId] = "Bench Press",
         };

        // Act
        var result = _service.BuildComparisonReport(currentExercises, previousScores, exerciseDetails);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            var comparison = result[0].SeriesComparisons[0];
            Assert.That(comparison.CurrentResult.Weight, Is.EqualTo(80));
            Assert.That(comparison.CurrentResult.Reps, Is.EqualTo(10));
            Assert.That(comparison.PreviousResult, Is.Not.Null);
            Assert.That(comparison.PreviousResult!.Weight, Is.EqualTo(75));
            Assert.That(comparison.PreviousResult.Reps, Is.EqualTo(8));
        });
    }

    // Test double — BuildComparisonReport does not use any dependencies
    private sealed class NullTrainingServiceDependencies : ITrainingServiceDependencies
    {
        public Application.Repositories.IUserRepository UserRepository => null!;
        public Application.Repositories.IGymRepository GymRepository => null!;
        public Application.Repositories.ITrainingRepository TrainingRepository => null!;
        public Application.Repositories.IExerciseRepository ExerciseRepository => null!;
        public Application.Repositories.IExerciseScoreRepository ExerciseScoreRepository => null!;
        public Application.Repositories.ITrainingExerciseScoreRepository TrainingExerciseScoreRepository => null!;
        public BackgroundWorker.Common.ICommandDispatcher CommandDispatcher => null!;
        public Application.Repositories.IEloRegistryRepository EloRepository => null!;
        public Application.Services.IRankService RankService => null!;
        public Application.Repositories.IUnitOfWork UnitOfWork => null!;
    }
}
