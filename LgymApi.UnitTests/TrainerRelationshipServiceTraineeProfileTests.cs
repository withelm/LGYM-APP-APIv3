using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainerRelationshipServiceTraineeProfileTests
{
    [Test]
    public async Task GetCurrentTrainerAsync_WhenNoActiveLink_ReturnsNotFound()
    {
        var trainee = CreateUser();
        var dependencies = CreateDependencies();
        dependencies.TrainerRelationshipRepository
            .FindActiveLinkByTraineeIdAsync(trainee.Id, Arg.Any<CancellationToken>())
            .Returns((TrainerTraineeLink?)null);
        var service = new TrainerRelationshipService(dependencies);

        var result = await service.GetCurrentTrainerAsync(trainee);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<TrainerRelationshipNotFoundError>();
    }

    [Test]
    public async Task GetCurrentTrainerAsync_WhenTrainerExists_ReturnsProfile()
    {
        var trainee = CreateUser();
        var trainer = CreateUser();
        var linkedAt = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
        var link = new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            CreatedAt = linkedAt
        };
        var dependencies = CreateDependencies();
        dependencies.TrainerRelationshipRepository
            .FindActiveLinkByTraineeIdAsync(trainee.Id, Arg.Any<CancellationToken>())
            .Returns(link);
        dependencies.UserRepository
            .FindByIdAsync(trainer.Id, Arg.Any<CancellationToken>())
            .Returns(trainer);
        var service = new TrainerRelationshipService(dependencies);

        var result = await service.GetCurrentTrainerAsync(trainee);

        result.IsSuccess.Should().BeTrue();
        result.Value.TrainerId.Should().Be(trainer.Id);
        result.Value.Name.Should().Be(trainer.Name);
        result.Value.Email.Should().Be(trainer.Email);
        result.Value.LinkedAt.Should().Be(linkedAt);
    }

    private static User CreateUser()
        => new()
        {
            Id = Id<User>.New(),
            Name = "User",
            Email = "user@example.com",
            ProfileRank = "Rookie"
        };

    private static ITrainerRelationshipServiceDependencies CreateDependencies()
    {
        var dependencies = Substitute.For<ITrainerRelationshipServiceDependencies>();
        dependencies.UserRepository.Returns(Substitute.For<IUserRepository>());
        dependencies.RoleRepository.Returns(Substitute.For<IRoleRepository>());
        dependencies.TrainerRelationshipRepository.Returns(Substitute.For<ITrainerRelationshipRepository>());
        dependencies.PlanRepository.Returns(Substitute.For<IPlanRepository>());
        dependencies.CommandDispatcher.Returns(Substitute.For<ICommandDispatcher>());
        dependencies.TrainingService.Returns(Substitute.For<ITrainingService>());
        dependencies.ExerciseScoresService.Returns(Substitute.For<IExerciseScoresService>());
        dependencies.EloRegistryService.Returns(Substitute.For<IEloRegistryService>());
        dependencies.MainRecordsService.Returns(Substitute.For<IMainRecordsService>());
        dependencies.UnitOfWork.Returns(Substitute.For<IUnitOfWork>());
        dependencies.Logger.Returns(Substitute.For<ILogger<TrainerRelationshipService>>());
        return dependencies;
    }
}
