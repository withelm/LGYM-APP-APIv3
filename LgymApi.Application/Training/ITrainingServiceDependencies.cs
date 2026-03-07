using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;

namespace LgymApi.Application.Features.Training;

public interface ITrainingServiceDependencies
{
    IUserRepository UserRepository { get; }
    IGymRepository GymRepository { get; }
    ITrainingRepository TrainingRepository { get; }
    IExerciseRepository ExerciseRepository { get; }
    IExerciseScoreRepository ExerciseScoreRepository { get; }
    ITrainingExerciseScoreRepository TrainingExerciseScoreRepository { get; }
    ICommandDispatcher CommandDispatcher { get; }
    IEloRegistryRepository EloRepository { get; }
    IRankService RankService { get; }
    IUnitOfWork UnitOfWork { get; }
}

internal sealed class TrainingServiceDependencies : ITrainingServiceDependencies
{
    public TrainingServiceDependencies(
        IUserRepository userRepository,
        IGymRepository gymRepository,
        ITrainingRepository trainingRepository,
        IExerciseRepository exerciseRepository,
        IExerciseScoreRepository exerciseScoreRepository,
        ITrainingExerciseScoreRepository trainingExerciseScoreRepository,
        ICommandDispatcher commandDispatcher,
        IEloRegistryRepository eloRepository,
        IRankService rankService,
        IUnitOfWork unitOfWork)
    {
        UserRepository = userRepository;
        GymRepository = gymRepository;
        TrainingRepository = trainingRepository;
        ExerciseRepository = exerciseRepository;
        ExerciseScoreRepository = exerciseScoreRepository;
        TrainingExerciseScoreRepository = trainingExerciseScoreRepository;
        CommandDispatcher = commandDispatcher;
        EloRepository = eloRepository;
        RankService = rankService;
        UnitOfWork = unitOfWork;
    }

    public IUserRepository UserRepository { get; }
    public IGymRepository GymRepository { get; }
    public ITrainingRepository TrainingRepository { get; }
    public IExerciseRepository ExerciseRepository { get; }
    public IExerciseScoreRepository ExerciseScoreRepository { get; }
    public ITrainingExerciseScoreRepository TrainingExerciseScoreRepository { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public IEloRegistryRepository EloRepository { get; }
    public IRankService RankService { get; }
    public IUnitOfWork UnitOfWork { get; }
}
