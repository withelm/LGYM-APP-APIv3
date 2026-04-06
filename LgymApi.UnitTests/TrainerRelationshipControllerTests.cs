using LgymApi.Api;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerRelationshipControllerTests
{
    [Test]
    public async Task CreateInvitation_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.CreateInvitation(new CreateTrainerInvitationRequest { TraineeId = "not-a-guid" });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetTraineeTrainingDates_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetTraineeTrainingDates("invalid-id");

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetTraineeExerciseScoresChartData_WithInvalidTraineeId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetTraineeExerciseScoresChartData("invalid-id", new ExerciseScoresChartRequestDto { ExerciseId = Id<Exercise>.New().ToString() });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetTraineeExerciseScoresChartData_WithInvalidExerciseId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetTraineeExerciseScoresChartData(Id<User>.New().ToString(), new ExerciseScoresChartRequestDto { ExerciseId = "invalid-exercise" });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task UpdateTraineePlan_WithInvalidPlanId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.UpdateTraineePlan(Id<User>.New().ToString(), "not-a-guid", new TrainerPlanFormRequest { Name = "Plan" });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
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
        public Task<Result<TrainerInvitationResult, AppError>> CreateInvitationAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<TrainerInvitationResult, AppError>> CreateInvitationByEmailAsync(User currentTrainer, string inviteeEmail, string preferredLanguage, string preferredTimeZone, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<TrainerInvitationResult>, AppError>> GetTrainerInvitationsAsync(User currentTrainer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Pagination<TrainerInvitationResult>, AppError>> GetInvitationsPaginatedAsync(User currentTrainer, FilterInput filterInput, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<TrainerDashboardTraineeListResult, AppError>> GetDashboardTraineesAsync(User currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<DateTime>, AppError>> GetTraineeTrainingDatesAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<TrainingByDateDetails>, AppError>> GetTraineeTrainingByDateAsync(User currentTrainer, Id<User> traineeId, DateTime createdAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<ExerciseScoresChartData>, AppError>> GetTraineeExerciseScoresChartDataAsync(User currentTrainer, Id<User> traineeId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<EloRegistryChartEntry>, AppError>> GetTraineeEloChartAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<MainRecord>, AppError>> GetTraineeMainRecordsHistoryAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<List<TrainerManagedPlanResult>, AppError>> GetTraineePlansAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<TrainerManagedPlanResult, AppError>> CreateTraineePlanAsync(User currentTrainer, Id<User> traineeId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<TrainerManagedPlanResult, AppError>> UpdateTraineePlanAsync(User currentTrainer, Id<User> traineeId, Id<Plan> planId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteTraineePlanAsync(User currentTrainer, Id<User> traineeId, Id<Plan> planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> AssignTraineePlanAsync(User currentTrainer, Id<User> traineeId, Id<Plan> planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> UnassignTraineePlanAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<TrainerManagedPlanResult, AppError>> GetActiveAssignedPlanAsync(User currentTrainee, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> AcceptInvitationAsync(User currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> RejectInvitationAsync(User currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> RevokeInvitationAsync(User currentTrainer, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> UnlinkTraineeAsync(User currentTrainer, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DetachFromTrainerAsync(User currentTrainee, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
