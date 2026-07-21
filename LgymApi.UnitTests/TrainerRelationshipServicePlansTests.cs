using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Repositories;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerRelationshipServicePlansTests
{
    [Test]
    public async Task GetTraineePlansAsync_ReturnsOnlyTraineePlansSortedDescending()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var link = new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id };
        var oldPlan = new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Old", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var newPlan = new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "New", CreatedAt = DateTimeOffset.UtcNow };
        var trainerTemplate = new Plan { Id = Id<Plan>.New(), UserId = trainer.Id, Name = "Template", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) };
        var deps = CreateDependencies();
        deps.RoleRepository.UserHasRoleAsync(trainer.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        deps.TrainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(trainer.Id, trainee.Id, Arg.Any<CancellationToken>()).Returns(link);
        deps.PlanRepository.GetByUserIdAsync(trainee.Id, Arg.Any<CancellationToken>()).Returns([oldPlan, newPlan]);
        deps.PlanRepository.GetByUserIdAsync(trainer.Id, Arg.Any<CancellationToken>()).Returns([trainerTemplate]);
        var service = new TrainerRelationshipService(deps);

        var result = await service.GetTraineePlansAsync(trainer, trainee.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(x => x.Name).Should().Equal("New", "Old");
    }

    [Test]
    public async Task CreateTraineePlanAsync_WhenNameBlank_ReturnsInvalidError()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var deps = CreateOwnedTraineeDependencies(trainer, trainee);
        var service = new TrainerRelationshipService(deps);

        var result = await service.CreateTraineePlanAsync(trainer, trainee.Id, "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidTrainerRelationshipError>();
    }

    [Test]
    public async Task CreateTraineePlanAsync_CreatesPlanForTrainee()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var deps = CreateOwnedTraineeDependencies(trainer, trainee);
        Plan? addedPlan = null;
        deps.PlanRepository.AddAsync(Arg.Do<Plan>(plan => addedPlan = plan), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var service = new TrainerRelationshipService(deps);

        var result = await service.CreateTraineePlanAsync(trainer, trainee.Id, "New plan");

        result.IsSuccess.Should().BeTrue();
        addedPlan.Should().NotBeNull();
        addedPlan!.UserId.Should().Be(trainer.Id);
    }

    [Test]
    public async Task AssignTraineePlanAsync_WhenTrainerOwnsPlan_ClonesAndAssignsClone()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var plan = new Plan { Id = Id<Plan>.New(), UserId = trainer.Id, Name = "Trainer Plan" };
        var clone = new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Cloned", IsActive = true };
        var deps = CreateOwnedTraineeDependencies(trainer, trainee);
        deps.PlanRepository.FindByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        deps.UserRepository.FindByIdAsync(trainee.Id, Arg.Any<CancellationToken>()).Returns(trainee);
        deps.PlanRepository.ClonePlanAsync(plan.Id, trainee.Id, true, Arg.Any<CancellationToken>()).Returns(clone);
        var service = new TrainerRelationshipService(deps);

        var result = await service.AssignTraineePlanAsync(trainer, trainee.Id, plan.Id);

        result.IsSuccess.Should().BeTrue();
        trainee.PlanId.Should().Be(clone.Id);
        await deps.PlanRepository.Received(1).ClearActivePlansAsync(trainee.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AssignTraineePlanAsync_WhenTraineeOwnsPlan_ActivatesExistingPlan()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var plan = new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Trainee Plan" };
        var deps = CreateOwnedTraineeDependencies(trainer, trainee);
        deps.PlanRepository.FindByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        deps.UserRepository.FindByIdAsync(trainee.Id, Arg.Any<CancellationToken>()).Returns(trainee);
        var service = new TrainerRelationshipService(deps);

        var result = await service.AssignTraineePlanAsync(trainer, trainee.Id, plan.Id);

        result.IsSuccess.Should().BeTrue();
        trainee.PlanId.Should().Be(plan.Id);
        await deps.PlanRepository.Received(1).SetActivePlanAsync(trainee.Id, plan.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteTraineePlanAsync_WhenAssignedToTrainee_MarksDeletedAndClearsAssignedPlan()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var plan = new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Assigned Plan", IsActive = true };
        trainee.PlanId = plan.Id;

        var deps = CreateOwnedTraineeDependencies(trainer, trainee);
        deps.PlanRepository.FindByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        deps.UserRepository.FindByIdAsync(trainee.Id, Arg.Any<CancellationToken>()).Returns(trainee);
        var service = new TrainerRelationshipService(deps);

        var result = await service.DeleteTraineePlanAsync(trainer, trainee.Id, plan.Id);

        result.IsSuccess.Should().BeTrue();
        plan.IsDeleted.Should().BeTrue();
        plan.IsActive.Should().BeFalse();
        trainee.PlanId.Should().BeNull();
        await deps.PlanRepository.Received(1).UpdateAsync(plan, Arg.Any<CancellationToken>());
        await deps.UserRepository.Received(1).UpdateAsync(trainee, Arg.Any<CancellationToken>());
        await deps.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UnassignTraineePlanAsync_WhenTraineeExists_ClearsActivePlansAndAssignedPlan()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        trainee.PlanId = Id<Plan>.New();

        var deps = CreateOwnedTraineeDependencies(trainer, trainee);
        deps.UserRepository.FindByIdAsync(trainee.Id, Arg.Any<CancellationToken>()).Returns(trainee);
        var service = new TrainerRelationshipService(deps);

        var result = await service.UnassignTraineePlanAsync(trainer, trainee.Id);

        result.IsSuccess.Should().BeTrue();
        trainee.PlanId.Should().BeNull();
        await deps.PlanRepository.Received(1).ClearActivePlansAsync(trainee.Id, Arg.Any<CancellationToken>());
        await deps.UserRepository.Received(1).UpdateAsync(trainee, Arg.Any<CancellationToken>());
        await deps.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static User CreateUser()
        => new() { Id = Id<User>.New(), Name = "user", Email = $"{Id<User>.New()}@example.com", ProfileRank = "Rookie" };

    private static ITrainerRelationshipServiceDependencies CreateOwnedTraineeDependencies(User trainer, User trainee)
    {
        var deps = CreateDependencies();
        deps.RoleRepository.UserHasRoleAsync(trainer.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        deps.TrainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(trainer.Id, trainee.Id, Arg.Any<CancellationToken>())
            .Returns(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
        return deps;
    }

    private static ITrainerRelationshipServiceDependencies CreateDependencies()
    {
        var deps = Substitute.For<ITrainerRelationshipServiceDependencies>();
        deps.UserRepository.Returns(Substitute.For<IUserRepository>());
        deps.RoleRepository.Returns(Substitute.For<IRoleRepository>());
        deps.TrainerRelationshipRepository.Returns(Substitute.For<ITrainerRelationshipRepository>());
        deps.PlanRepository.Returns(Substitute.For<IPlanRepository>());
        deps.CommandDispatcher.Returns(Substitute.For<ICommandDispatcher>());
        deps.WorkoutProgressDashboardReadService.Returns(Substitute.For<LgymApi.Application.WorkoutProgress.Dashboard.IWorkoutProgressDashboardReadService>());
        deps.UnitOfWork.Returns(Substitute.For<IUnitOfWork>());
        deps.Logger.Returns(Substitute.For<ILogger<TrainerRelationshipService>>());
        return deps;
    }
}
