using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.WorkoutProgress.Dashboard;
using LgymApi.Application.WorkoutProgress.ProgressData;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Application.WorkoutProgress.TrainingExecution;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.TrainingPlanning.Contracts;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class WorkoutProgressDashboardReadServiceTests
{
    [Test]
    public async Task GetTrainingByDateAsync_MapsTrainingEntitiesToDashboardReadModels()
    {
        var trainingHistory = Substitute.For<ITrainingHistoryReadService>();
        var progress = Substitute.For<IWorkoutProgressReadWriteService>();
        var traineeId = Id<AccountReference>.New();
        var createdAt = DateTime.UtcNow;
        var planDayId = Id<PlanDayReference>.New();
        var exercise = new ProgressExerciseReadModel(Id<Exercise>.New(), "Bench", null, BodyParts.Chest, null, null, null);
        var score = new WorkoutExerciseScoreReadModel(Id<ExerciseScore>.New(), exercise.Id, 80, WeightUnits.Kilograms, 8, 1, null);
        var training = new TrainingByDateDetails
        {
            Id = Id<Training>.New(),
            TypePlanDayId = planDayId,
            CreatedAt = createdAt,
            PlanDay = new PlanDayReferenceReadModel(planDayId, Id<PlanReference>.New(), "Push", true, false, traineeId, PlanPurpose.Personal),
            Gym = "Gym",
            Exercises =
            [
                new EnrichedExercise
                {
                    ExerciseScoreId = score.Id,
                    ExerciseDetails = exercise,
                    ScoresDetails = [score]
                }
            ]
        };
        var dateRange = UtcTimeRange.ForLegacyUtcDay(createdAt);
        trainingHistory.GetTrainingByDateAsync(traineeId, dateRange, Arg.Any<CancellationToken>())
            .Returns(Result<List<TrainingByDateDetails>, AppError>.Success([training]));
        IReadOnlyList<string> cultures = ["pl-PL", "pl"];
        IReadOnlyDictionary<Id<Exercise>, string> translations = new Dictionary<Id<Exercise>, string> { [exercise.Id] = "Wyciskanie" };
        progress.GetExerciseDisplayNamesAsync(
                Arg.Is<IEnumerable<Id<Exercise>>>(ids => ids.SequenceEqual(new[] { exercise.Id })),
                cultures,
                Arg.Any<CancellationToken>())
            .Returns(translations);
        var service = new WorkoutProgressDashboardReadService(trainingHistory, progress);

        var result = await service.GetTrainingByDateAsync(traineeId, dateRange, cultures);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Translations, Is.SameAs(translations));
        Assert.That(result.Value.Trainings, Has.Exactly(1).Items);
        Assert.That(result.Value.Trainings[0].Id, Is.EqualTo(training.Id.ToString()));
        var assertedObject42 = result.Value.Trainings[0].PlanDay;
        Assert.That(assertedObject42, Is.Not.Null);
        Assert.That(assertedObject42!.Id, Is.EqualTo(planDayId.ToString()));
        Assert.That(assertedObject42!.Name, Is.EqualTo("Push"));
        var assertedObject43 = result.Value.Trainings[0].Exercises[0].ExerciseDetails;
        Assert.That(assertedObject43, Is.Not.Null);
        Assert.That(assertedObject43!.Id, Is.EqualTo(exercise.Id.ToString()));
        Assert.That(assertedObject43!.Name, Is.EqualTo("Bench"));
        var assertedObject44 = result.Value.Trainings[0].Exercises[0].ScoresDetails[0];
        Assert.That(assertedObject44, Is.Not.Null);
        Assert.That(assertedObject44!.Id, Is.EqualTo(score.Id.ToString()));
        Assert.That(assertedObject44!.ExerciseId, Is.EqualTo(exercise.Id.ToString()));
        Assert.That(assertedObject44!.Weight, Is.EqualTo(80d));
        Assert.That(assertedObject44!.Reps, Is.EqualTo(8d));
        Assert.That(assertedObject44!.Series, Is.EqualTo(1));
    }

    [Test]
    public async Task ProgressReads_DelegateAuthorizedTraineeIdToOwnerContracts()
    {
        var trainingHistory = Substitute.For<ITrainingHistoryReadService>();
        var progress = Substitute.For<IWorkoutProgressReadWriteService>();
        var traineeId = Id<AccountReference>.New();
        var exerciseId = Id<Exercise>.New();
        trainingHistory.GetTrainingDatesAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Result<List<DateTime>, AppError>.Success([]));
        progress.GetExerciseScoreChartAsync(traineeId, exerciseId, Arg.Any<CancellationToken>()).Returns(Result<List<ExerciseScoreChartPoint>, AppError>.Success([]));
        progress.GetEloChartAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Result<List<EloChartPoint>, AppError>.Success([]));
        progress.GetMainRecordHistoryAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Result<List<MainRecordReadModel>, AppError>.Success([]));
        progress.GetBestMainRecordsAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Result<List<MainRecordBestReadModel>, AppError>.Success([]));
        var service = new WorkoutProgressDashboardReadService(trainingHistory, progress);

        await service.GetTrainingDatesAsync(traineeId);
        await service.GetExerciseScoreChartAsync(traineeId, exerciseId.ToString());
        await service.GetEloChartAsync(traineeId);
        await service.GetMainRecordHistoryAsync(traineeId);
        await service.GetBestMainRecordsAsync(traineeId);

        await trainingHistory.Received(1).GetTrainingDatesAsync(traineeId, Arg.Any<CancellationToken>());
        await progress.Received(1).GetExerciseScoreChartAsync(traineeId, exerciseId, Arg.Any<CancellationToken>());
        await progress.Received(1).GetEloChartAsync(traineeId, Arg.Any<CancellationToken>());
        await progress.Received(1).GetMainRecordHistoryAsync(traineeId, Arg.Any<CancellationToken>());
        await progress.Received(1).GetBestMainRecordsAsync(traineeId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTrainingByDateAsync_PreservesOwnerFailure()
    {
        var trainingHistory = Substitute.For<ITrainingHistoryReadService>();
        var progress = Substitute.For<IWorkoutProgressReadWriteService>();
        var traineeId = Id<AccountReference>.New();
        var error = new TrainingNotFoundError("missing");
        trainingHistory.GetTrainingByDateAsync(traineeId, Arg.Any<UtcTimeRange>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<TrainingByDateDetails>, AppError>.Failure(error));
        var service = new WorkoutProgressDashboardReadService(trainingHistory, progress);

        var result = await service.GetTrainingByDateAsync(traineeId, UtcTimeRange.ForLegacyUtcDay(DateTime.UtcNow), []);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.SameAs(error));
        await progress.DidNotReceiveWithAnyArgs().GetExerciseDisplayNamesAsync(default!, default!, default);
    }
}
