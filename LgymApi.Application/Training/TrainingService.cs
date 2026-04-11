using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Application.Units;
using LgymApi.BackgroundWorker.Common;

namespace LgymApi.Application.Features.Training;

public sealed partial class TrainingService : ITrainingService
{
    private readonly IUserRepository _userRepository;
    private readonly IGymRepository _gymRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;
    private readonly ITrainingExerciseScoreRepository _trainingExerciseScoreRepository;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IEloRegistryRepository _eloRepository;
    private readonly IRankService _rankService;
    private readonly IUnitOfWork _unitOfWork;

    public TrainingService(ITrainingServiceDependencies dependencies)
    {
        _userRepository = dependencies.UserRepository;
        _gymRepository = dependencies.GymRepository;
        _trainingRepository = dependencies.TrainingRepository;
        _exerciseRepository = dependencies.ExerciseRepository;
        _exerciseScoreRepository = dependencies.ExerciseScoreRepository;
        _trainingExerciseScoreRepository = dependencies.TrainingExerciseScoreRepository;
        _commandDispatcher = dependencies.CommandDispatcher;
        _eloRepository = dependencies.EloRepository;
        _rankService = dependencies.RankService;
        _unitOfWork = dependencies.UnitOfWork;
    }
}
