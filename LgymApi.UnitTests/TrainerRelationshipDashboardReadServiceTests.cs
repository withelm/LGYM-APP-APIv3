using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Dashboard;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerRelationshipDashboardReadServiceTests
{
    [Test]
    public async Task GetTraineeExerciseScoresChartDataAsync_AuthorizesRelationshipBeforeCallingWorkoutProgress()
    {
        var dependencies = Substitute.For<ITrainerRelationshipServiceDependencies>();
        var roles = Substitute.For<IRoleRepository>();
        var relationships = Substitute.For<ITrainerRelationshipRepository>();
        var dashboard = Substitute.For<IWorkoutProgressDashboardReadService>();
        var trainer = new User { Id = Id<User>.New() };
        var traineeId = Id<User>.New();
        var exerciseId = Id<Exercise>.New().ToString();
        dependencies.RoleRepository.Returns(roles);
        dependencies.TrainerRelationshipRepository.Returns(relationships);
        dependencies.WorkoutProgressDashboardReadService.Returns(dashboard);
        roles.UserHasRoleAsync(trainer.Id, AuthConstants.Roles.Trainer, Arg.Any<CancellationToken>()).Returns(true);
        relationships.FindActiveLinkByTrainerAndTraineeAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns(new TrainerTraineeLink { TrainerId = trainer.Id, TraineeId = traineeId });
        dashboard.GetExerciseScoreChartAsync(traineeId, exerciseId, Arg.Any<CancellationToken>())
            .Returns(Result<List<ExerciseScoreChartPoint>, AppError>.Success([]));
        var service = new TrainerRelationshipService(dependencies);

        var result = await service.GetTraineeExerciseScoresChartDataAsync(trainer, traineeId, exerciseId);

        result.IsSuccess.Should().BeTrue();
        await dashboard.Received(1).GetExerciseScoreChartAsync(traineeId, exerciseId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetTraineeMainRecordsHistoryAsync_WhenRelationshipIsMissing_DoesNotCallWorkoutProgress()
    {
        var dependencies = Substitute.For<ITrainerRelationshipServiceDependencies>();
        var roles = Substitute.For<IRoleRepository>();
        var relationships = Substitute.For<ITrainerRelationshipRepository>();
        var dashboard = Substitute.For<IWorkoutProgressDashboardReadService>();
        var trainer = new User { Id = Id<User>.New() };
        var traineeId = Id<User>.New();
        dependencies.RoleRepository.Returns(roles);
        dependencies.TrainerRelationshipRepository.Returns(relationships);
        dependencies.WorkoutProgressDashboardReadService.Returns(dashboard);
        roles.UserHasRoleAsync(trainer.Id, AuthConstants.Roles.Trainer, Arg.Any<CancellationToken>()).Returns(true);
        relationships.FindActiveLinkByTrainerAndTraineeAsync(trainer.Id, traineeId, Arg.Any<CancellationToken>())
            .Returns((TrainerTraineeLink?)null);
        var service = new TrainerRelationshipService(dependencies);

        var result = await service.GetTraineeMainRecordsHistoryAsync(trainer, traineeId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<TrainerRelationshipNotFoundError>();
        await dashboard.DidNotReceive().GetMainRecordHistoryAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>());
    }
}
