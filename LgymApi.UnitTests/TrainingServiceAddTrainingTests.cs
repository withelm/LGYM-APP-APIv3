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

        var deps = Substitute.For<ITrainingServiceDependencies>();
        deps.UserRepository.Returns(_userRepository);
        deps.GymRepository.Returns(_gymRepository);
        deps.TrainingRepository.Returns(_trainingRepository);
        deps.ExerciseRepository.Returns(_exerciseRepository);
        deps.ExerciseScoreRepository.Returns(_exerciseScoreRepository);
        deps.TrainingExerciseScoreRepository.Returns(_trainingExerciseScoreRepository);
        deps.CommandDispatcher.Returns(_commandDispatcher);
        deps.EloRepository.Returns(_eloRepository);
        deps.RankService.Returns(_rankService);
        deps.UnitOfWork.Returns(_unitOfWork);

        _service = new TrainingService(deps);
    }

    [Test]
    public async Task AddTrainingAsync_WhenEloEntryIsNull_ReturnsInternalServerError()
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
    public void AddTrainingAsync_WhenRepositoryThrowsException_PropagatesException()
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
}
