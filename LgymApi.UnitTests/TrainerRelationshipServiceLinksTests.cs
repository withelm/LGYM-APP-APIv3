using FluentAssertions;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Repositories;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerRelationshipServiceLinksTests
{
    [Test]
    public async Task DetachFromTrainerAsync_WhenLinkExists_RemovesLinkAndEnqueuesNotification()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var link = new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id };
        var deps = CreateDependencies();
        var operations = new List<string>();
        deps.TrainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(trainee.Id, Arg.Any<CancellationToken>()).Returns(link);
        deps.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1))
            .AndDoes(_ => operations.Add("commit"));
        deps.CommandDispatcher.EnqueueAsync(Arg.Any<TrainerRelationshipEndedInAppNotificationCommand>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => operations.Add("enqueue"));
        var service = new TrainerRelationshipService(deps);

        var result = await service.DetachFromTrainerAsync(trainee);

        result.IsSuccess.Should().BeTrue();
        await deps.TrainerRelationshipRepository.Received(1).RemoveLinkAsync(link, Arg.Any<CancellationToken>());
        await deps.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await deps.CommandDispatcher.Received(1).EnqueueAsync(Arg.Is<TrainerRelationshipEndedInAppNotificationCommand>(command =>
            command.TrainerId == trainer.Id && command.TraineeId == trainee.Id));
        operations.Should().Equal("commit", "enqueue");
    }

    [Test]
    public async Task UnlinkTraineeAsync_WhenTrainerOwnsTrainee_RemovesLinkWithoutEnqueuingRelationshipEndedNotification()
    {
        var trainer = CreateUser();
        var trainee = CreateUser();
        var link = new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id };
        var deps = CreateDependencies();
        deps.RoleRepository.UserHasRoleAsync(trainer.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        deps.TrainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(trainer.Id, trainee.Id, Arg.Any<CancellationToken>()).Returns(link);
        var service = new TrainerRelationshipService(deps);

        var result = await service.UnlinkTraineeAsync(trainer, trainee.Id);

        result.IsSuccess.Should().BeTrue();
        await deps.TrainerRelationshipRepository.Received(1).RemoveLinkAsync(link, Arg.Any<CancellationToken>());
        await deps.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await deps.CommandDispatcher.DidNotReceive().EnqueueAsync(Arg.Any<TrainerRelationshipEndedInAppNotificationCommand>());
    }

    private static User CreateUser()
        => new() { Id = Id<User>.New(), Name = "user", Email = $"{Id<User>.New()}@example.com", ProfileRank = "Rookie" };

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
