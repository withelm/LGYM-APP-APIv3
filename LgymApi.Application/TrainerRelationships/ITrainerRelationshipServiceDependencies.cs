using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipServiceDependencies
{
    IUserRepository UserRepository { get; }
    IRoleRepository RoleRepository { get; }
    ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    IPlanRepository PlanRepository { get; }
    ICommandDispatcher CommandDispatcher { get; }
    ITrainingService TrainingService { get; }
    IExerciseScoresService ExerciseScoresService { get; }
    IEloRegistryService EloRegistryService { get; }
    IMainRecordsService MainRecordsService { get; }
    IUnitOfWork UnitOfWork { get; }
    ILogger<TrainerRelationshipService> Logger { get; }
}

internal sealed class TrainerRelationshipServiceDependencies : ITrainerRelationshipServiceDependencies
{
    public TrainerRelationshipServiceDependencies(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        IPlanRepository planRepository,
        ICommandDispatcher commandDispatcher,
        ITrainingService trainingService,
        IExerciseScoresService exerciseScoresService,
        IEloRegistryService eloRegistryService,
        IMainRecordsService mainRecordsService,
        IUnitOfWork unitOfWork,
        ILogger<TrainerRelationshipService> logger)
    {
        UserRepository = userRepository;
        RoleRepository = roleRepository;
        TrainerRelationshipRepository = trainerRelationshipRepository;
        PlanRepository = planRepository;
        CommandDispatcher = commandDispatcher;
        TrainingService = trainingService;
        ExerciseScoresService = exerciseScoresService;
        EloRegistryService = eloRegistryService;
        MainRecordsService = mainRecordsService;
        UnitOfWork = unitOfWork;
        Logger = logger;
    }

    public IUserRepository UserRepository { get; }
    public IRoleRepository RoleRepository { get; }
    public ITrainerRelationshipRepository TrainerRelationshipRepository { get; }
    public IPlanRepository PlanRepository { get; }
    public ICommandDispatcher CommandDispatcher { get; }
    public ITrainingService TrainingService { get; }
    public IExerciseScoresService ExerciseScoresService { get; }
    public IEloRegistryService EloRegistryService { get; }
    public IMainRecordsService MainRecordsService { get; }
    public IUnitOfWork UnitOfWork { get; }
    public ILogger<TrainerRelationshipService> Logger { get; }
}
