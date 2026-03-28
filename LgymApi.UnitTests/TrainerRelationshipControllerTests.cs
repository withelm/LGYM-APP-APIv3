using LgymApi.Api;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerRelationshipControllerTests
{
    [Test]
    public void CreateInvitation_WithInvalidTraineeId_ThrowsBadRequest()
    {
        var controller = CreateController();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await controller.CreateInvitation(new CreateTrainerInvitationRequest { TraineeId = "not-a-guid" }));

        Assert.That(exception!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void GetTraineeTrainingDates_WithInvalidTraineeId_ThrowsBadRequest()
    {
        var controller = CreateController();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await controller.GetTraineeTrainingDates("invalid-id"));

        Assert.That(exception!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void GetTraineeExerciseScoresChartData_WithInvalidTraineeId_ThrowsBadRequest()
    {
        var controller = CreateController();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await controller.GetTraineeExerciseScoresChartData("invalid-id", new ExerciseScoresChartRequestDto { ExerciseId = Id<Exercise>.New().ToString() }));

        Assert.That(exception!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void GetTraineeExerciseScoresChartData_WithInvalidExerciseId_ThrowsBadRequest()
    {
        var controller = CreateController();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await controller.GetTraineeExerciseScoresChartData(Id<User>.New().ToString(), new ExerciseScoresChartRequestDto { ExerciseId = "invalid-exercise" }));

        Assert.That(exception!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void UpdateTraineePlan_WithInvalidPlanId_ThrowsBadRequest()
    {
        var controller = CreateController();

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await controller.UpdateTraineePlan(Id<User>.New().ToString(), "not-a-guid", new TrainerPlanFormRequest { Name = "Plan" }));

        Assert.That(exception!.StatusCode, Is.EqualTo(400));
    }

    private static TrainerRelationshipController CreateController()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        return new TrainerRelationshipController(new StubTrainerRelationshipService(), mapper);
    }

    private sealed class StubTrainerRelationshipService : ITrainerRelationshipService
    {
        public Task<TrainerInvitationResult> CreateInvitationAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(User currentTrainer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(User currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<DateTime>> GetTraineeTrainingDatesAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<TrainingByDateDetails>> GetTraineeTrainingByDateAsync(User currentTrainer, Id<User> traineeId, DateTime createdAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScoresChartData>> GetTraineeExerciseScoresChartDataAsync(User currentTrainer, Id<User> traineeId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<EloRegistryChartEntry>> GetTraineeEloChartAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MainRecord>> GetTraineeMainRecordsHistoryAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<TrainerManagedPlanResult>> GetTraineePlansAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerManagedPlanResult> CreateTraineePlanAsync(User currentTrainer, Id<User> traineeId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerManagedPlanResult> UpdateTraineePlanAsync(User currentTrainer, Id<User> traineeId, Id<Plan> planId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteTraineePlanAsync(User currentTrainer, Id<User> traineeId, Id<Plan> planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AssignTraineePlanAsync(User currentTrainer, Id<User> traineeId, Id<Plan> planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UnassignTraineePlanAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerManagedPlanResult> GetActiveAssignedPlanAsync(User currentTrainee, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AcceptInvitationAsync(User currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RejectInvitationAsync(User currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UnlinkTraineeAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DetachFromTrainerAsync(User currentTrainee, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
