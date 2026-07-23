using LgymApi.Domain.Entities;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class WorkoutProgressOwnershipTests
{
    private static readonly Type[] WorkoutProgressEntityTypes =
    {
        typeof(Exercise),
        typeof(ExerciseTranslation),
        typeof(Training),
        typeof(TrainingExerciseScore),
        typeof(ExerciseScore),
        typeof(Measurement),
        typeof(MainRecord),
        typeof(Gym),
        typeof(Address),
        typeof(EloRegistry)
    };

    private static readonly Type[] TrainingPlanningEntityTypes =
    {
        typeof(Plan),
        typeof(PlanDay),
        typeof(PlanDayExercise)
    };

    [TestCase("/LgymApi.Application/WorkoutProgress/Contracts/ProgressContract.cs")]
    [TestCase("/LgymApi.Application/Training/TrainingService.cs")]
    [TestCase("/LgymApi.Application/Exercise/ExerciseService.cs")]
    [TestCase("/LgymApi.Application/ExerciseScores/ExerciseScoresService.cs")]
    [TestCase("/LgymApi.Application/Gym/GymService.cs")]
    [TestCase("/LgymApi.Application/Measurements/MeasurementsService.cs")]
    [TestCase("/LgymApi.Application/MainRecords/MainRecordsService.cs")]
    [TestCase("/LgymApi.Application/EloRegistry/EloRegistryService.cs")]
    public void Workout_Progress_Application_Paths_Should_Have_Canonical_Ownership(string path)
    {
        Assert.That(
            ArchitectureTestHelpers.GetCanonicalModuleNameFromPath(path),
            Is.EqualTo(PersistedEntityOwnershipCatalog.WorkoutProgressModuleName));
    }

    [Test]
    public void Workout_Progress_And_Training_Planning_Persisted_Entity_Ownership_Should_Remain_Separated()
    {
        var ownerByEntityType = PersistedEntityOwnershipCatalog.Entries
            .ToDictionary(entry => entry.EntityType, entry => entry.Owner);

        Assert.Multiple(() =>
        {
            Assert.That(WorkoutProgressEntityTypes, Has.Length.EqualTo(10));
            Assert.That(TrainingPlanningEntityTypes, Has.Length.EqualTo(3));

            foreach (var entityType in WorkoutProgressEntityTypes)
            {
                Assert.That(
                    ownerByEntityType[entityType],
                    Is.EqualTo(PersistedEntityOwnershipCatalog.WorkoutProgressModuleName),
                    $"{entityType.Name} must remain owned by Workout & Progress.");
            }

            foreach (var entityType in TrainingPlanningEntityTypes)
            {
                Assert.That(
                    ownerByEntityType[entityType],
                    Is.EqualTo(PersistedEntityOwnershipCatalog.TrainingPlanningModuleName),
                    $"{entityType.Name} must remain owned by Training Planning.");
            }
        });
    }

    [Test]
    public void Ownership_Catalog_Should_Reject_Completed_Training_Assigned_To_Training_Planning()
    {
        var invalidEntries = PersistedEntityOwnershipCatalog.Entries
            .Select(entry => entry.EntityType == typeof(Training)
                ? new PersistedEntityOwnership(entry.EntityType, PersistedEntityOwnershipCatalog.TrainingPlanningModuleName)
                : entry)
            .ToList();
        var persistedEntityTypes = PersistedEntityOwnershipCatalog.Entries.Select(entry => entry.EntityType);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PersistedEntityOwnershipCatalog.Validate(invalidEntries, persistedEntityTypes));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("Training Planning expected 3"));
            Assert.That(exception.Message, Does.Contain("Workout & Progress expected 10"));
        });
    }
}
