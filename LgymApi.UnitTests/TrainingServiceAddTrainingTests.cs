using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingServiceAddTrainingTests
{
    private ITrainingServiceDependencies _deps = null!;
    private IUserRepository _userRepository = null!;
    private IGymRepository _gymRepository = null!;
    private ITrainingRepository _trainingRepository = null!;
    private IExerciseRepository _exerciseRepository = null!;
    private IExerciseScoreRepository _exerciseScoreRepository = null!;
    private ITrainingExerciseScoreRepository _trainingExerciseScoreRepository = null!;
    private ICommandDispatcher _commandDispatcher = null!;
    private IEloRegistryRepository _eloRepository = null!;
    private IRankService _rankService = null!;
    private IUnitOfWork _unitOfWork = null!;
    private TrainingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _gymRepository = Substitute.For<IGymRepository>();
        _trainingRepository = Substitute.For<ITrainingRepository>();
        _exerciseRepository = Substitute.For<IExerciseRepository>();
        _exerciseScoreRepository = Substitute.For<IExerciseScoreRepository>();
        _trainingExerciseScoreRepository = Substitute.For<ITrainingExerciseScoreRepository>();
        _commandDispatcher = Substitute.For<ICommandDispatcher>();
        _eloRepository = Substitute.For<IEloRegistryRepository>();
        _rankService = Substitute.For<IRankService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _deps = Substitute.For<ITrainingServiceDependencies>();
        _deps.UserRepository.Returns(_userRepository);
        _deps.GymRepository.Returns(_gymRepository);
        _deps.TrainingRepository.Returns(_trainingRepository);
        _deps.ExerciseRepository.Returns(_exerciseRepository);
        _deps.ExerciseScoreRepository.Returns(_exerciseScoreRepository);
        _deps.TrainingExerciseScoreRepository.Returns(_trainingExerciseScoreRepository);
        _deps.CommandDispatcher.Returns(_commandDispatcher);
        _deps.EloRepository.Returns(_eloRepository);
        _deps.RankService.Returns(_rankService);
        _deps.UnitOfWork.Returns(_unitOfWork);

        _service = new TrainingService(_deps);
    }

    [Test]
    public async Task Should_ReturnInternalServerError_When_EloEntryIsNull()
    {
        // Arrange
        var userId = Id<User>.New();
        var gymId = Id<Gym>.New();
        var planDayId = Id<PlanDay>.New();
        var exerciseId = Id<Exercise>.New();

        var user = new User
        {
            Id = userId,
            Name = "TestUser",
            Email = "test@example.com",
            ProfileRank = "Junior 1"
        };

        var gym = new Gym { Id = gymId, UserId = userId, Name = "TestGym" };

        var exercises = new List<TrainingExerciseInput>
        {
            new() { ExerciseId = exerciseId, Series = 1, Reps = 10, Weight = 80, Unit = WeightUnits.Kilograms }
        };

        var input = new AddTrainingInput(gymId, planDayId, DateTime.UtcNow, exercises);

        _userRepository.FindByIdAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _gymRepository.FindByIdAsync(Arg.Any<Id<Gym>>(), Arg.Any<CancellationToken>())
            .Returns(gym);
        _exerciseRepository.GetByIdsAsync(Arg.Any<List<Id<Exercise>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Exercise> { new() { Id = exerciseId, Name = "Bench Press" } });
        _exerciseScoreRepository.GetByUserAndExercisesAsync(Arg.Any<Id<User>>(), Arg.Any<List<Id<Exercise>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ExerciseScore>());

        var transaction = Substitute.For<IUnitOfWorkTransaction>();
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(transaction);

        _eloRepository.GetLatestEntryAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Returns((EloRegistry?)null);

        // Act
        var result = await _service.AddTrainingAsync(userId, input);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InternalServerError>());
            Assert.That(result.Error.Message, Is.EqualTo(Messages.TryAgain));
        });
    }

    [Test]
    public void Should_PropagateException_When_RepositoryThrowsException()
    {
        // Arrange
        var userId = Id<User>.New();
        var gymId = Id<Gym>.New();
        var planDayId = Id<PlanDay>.New();

        var input = new AddTrainingInput(gymId, planDayId, DateTime.UtcNow, new List<TrainingExerciseInput>());

        _userRepository.FindByIdAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddTrainingAsync(userId, input));
        Assert.That(ex!.Message, Is.EqualTo("Database connection failed"));
    }

    [Test]
    public async Task Should_ReturnInvalidTrainingDataError_When_UserIdIsEmpty()
    {
        // Arrange
        var emptyUserId = Id<User>.Empty;
        var gymId = Id<Gym>.New();
        var planDayId = Id<PlanDay>.New();

        var input = new AddTrainingInput(gymId, planDayId, DateTime.UtcNow, new List<TrainingExerciseInput>());

        // Act
        var result = await _service.AddTrainingAsync(emptyUserId, input);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InvalidTrainingDataError>());
        });
    }

    [Test]
    public async Task Should_ReturnInvalidTrainingDataError_When_GymIdIsEmpty()
    {
        // Arrange
        var userId = Id<User>.New();
        var emptyGymId = Id<Gym>.Empty;
        var planDayId = Id<PlanDay>.New();

        var input = new AddTrainingInput(emptyGymId, planDayId, DateTime.UtcNow, new List<TrainingExerciseInput>());

        // Act
        var result = await _service.AddTrainingAsync(userId, input);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InvalidTrainingDataError>());
        });
    }

    [Test]
    public async Task Should_ReturnInvalidTrainingDataError_When_PlanDayIdIsEmpty()
    {
        // Arrange
        var userId = Id<User>.New();
        var gymId = Id<Gym>.New();
        var emptyPlanDayId = Id<PlanDay>.Empty;

        var input = new AddTrainingInput(gymId, emptyPlanDayId, DateTime.UtcNow, new List<TrainingExerciseInput>());

        // Act
        var result = await _service.AddTrainingAsync(userId, input);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InvalidTrainingDataError>());
        });
    }

    [Test]
    public async Task Should_ReturnInvalidTrainingDataError_When_ExerciseHasUnknownUnit()
    {
        // Arrange
        var userId = Id<User>.New();
        var gymId = Id<Gym>.New();
        var planDayId = Id<PlanDay>.New();
        var exerciseId = Id<Exercise>.New();

        var user = new User
        {
            Id = userId,
            Name = "TestUser",
            Email = "test@example.com",
            ProfileRank = "Junior 1"
        };

        var gym = new Gym { Id = gymId, UserId = userId, Name = "TestGym" };

        var exercises = new List<TrainingExerciseInput>
        {
            new() { ExerciseId = exerciseId, Series = 1, Reps = 10, Weight = 80, Unit = WeightUnits.Unknown }
        };

        var input = new AddTrainingInput(gymId, planDayId, DateTime.UtcNow, exercises);

        _userRepository.FindByIdAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _gymRepository.FindByIdAsync(Arg.Any<Id<Gym>>(), Arg.Any<CancellationToken>())
            .Returns(gym);
        _exerciseRepository.GetByIdsAsync(Arg.Any<List<Id<Exercise>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Exercise> { new() { Id = exerciseId, Name = "Bench Press" } });
        _exerciseScoreRepository.GetByUserAndExercisesAsync(Arg.Any<Id<User>>(), Arg.Any<List<Id<Exercise>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ExerciseScore>());

        var transaction = Substitute.For<IUnitOfWorkTransaction>();
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(transaction);

        // Act
        var result = await _service.AddTrainingAsync(userId, input);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InvalidTrainingDataError>());
        });

        // Verify transaction was rolled back
        await transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }
}
