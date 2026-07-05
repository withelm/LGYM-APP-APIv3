using FluentAssertions;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
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
        deps.TrainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(trainee.Id, Arg.Any<CancellationToken>()).Returns(link);
        var service = new TrainerRelationshipService(deps);

        var result = await service.DetachFromTrainerAsync(trainee);

        result.IsSuccess.Should().BeTrue();
        await deps.TrainerRelationshipRepository.Received(1).RemoveLinkAsync(link, Arg.Any<CancellationToken>());
        await deps.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await deps.CommandDispatcher.Received(1).EnqueueAsync(Arg.Is<TrainerRelationshipEndedInAppNotificationCommand>(command =>
            command.TrainerId == trainer.Id && command.TraineeId == trainee.Id));
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
        deps.TrainingService.Returns(Substitute.For<ITrainingService>());
        deps.ExerciseScoresService.Returns(Substitute.For<IExerciseScoresService>());
        deps.EloRegistryService.Returns(Substitute.For<IEloRegistryService>());
        deps.MainRecordsService.Returns(Substitute.For<IMainRecordsService>());
        deps.UnitOfWork.Returns(Substitute.For<IUnitOfWork>());
        deps.Logger.Returns(Substitute.For<ILogger<TrainerRelationshipService>>());
        return deps;
    }
}
