using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.WorkoutProgress.ProgressData;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseScoresServiceTests
{
    [Test]
    public async Task GetChart_ShouldDelegateMarkerIdAndPreserveProjection()
    {
        var accountId = Id<AccountReference>.New();
        var exerciseId = Id<Exercise>.New();
        var progress = Substitute.For<IWorkoutProgressReadWriteService>();
        progress.GetExerciseScoreChartAsync(accountId, exerciseId, Arg.Any<CancellationToken>())
            .Returns(Result<List<ExerciseScoreChartPoint>, AppError>.Success([new("entry", 120, "01/01", "Squat", exerciseId)]));

        var result = await new ExerciseScoresService(progress).GetExerciseScoresChartDataAsync(accountId, exerciseId);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That((result.Value).Where(item => item.Id == "entry" && item.ExerciseId == exerciseId), Has.Exactly(1).Items);
    }

    [Test]
    public async Task GetChart_ShouldPassThroughOwnerError()
    {
        var progress = Substitute.For<IWorkoutProgressReadWriteService>();
        var error = new BadRequestError("missing");
        progress.GetExerciseScoreChartAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<Id<Exercise>>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ExerciseScoreChartPoint>, AppError>.Failure(error));
        var result = await new ExerciseScoresService(progress).GetExerciseScoresChartDataAsync(Id<AccountReference>.New(), Id<Exercise>.New());
        Assert.That(result.Error, Is.SameAs(error));
    }
}
