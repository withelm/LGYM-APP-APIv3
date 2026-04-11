using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public sealed partial class TrainerRelationshipService : ITrainerRelationshipService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly IPlanRepository _planRepository;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ITrainingService _trainingService;
    private readonly IExerciseScoresService _exerciseScoresService;
    private readonly IEloRegistryService _eloRegistryService;
    private readonly IMainRecordsService _mainRecordsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainerRelationshipService> _logger;

    public TrainerRelationshipService(ITrainerRelationshipServiceDependencies dependencies)
    {
        _userRepository = dependencies.UserRepository;
        _roleRepository = dependencies.RoleRepository;
        _trainerRelationshipRepository = dependencies.TrainerRelationshipRepository;
        _planRepository = dependencies.PlanRepository;
        _commandDispatcher = dependencies.CommandDispatcher;
        _trainingService = dependencies.TrainingService;
        _exerciseScoresService = dependencies.ExerciseScoresService;
        _eloRegistryService = dependencies.EloRegistryService;
        _mainRecordsService = dependencies.MainRecordsService;
        _unitOfWork = dependencies.UnitOfWork;
        _logger = dependencies.Logger;
    }

    private async Task<Result<Unit, AppError>> EnsureTrainerAsync(UserEntity currentTrainer, CancellationToken cancellationToken)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipForbiddenError(Messages.TrainerRoleRequired));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<TrainerTraineeLink, AppError>> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<TrainerTraineeLink, AppError>.Failure(ensureTrainerResult.Error);
        }

        if (traineeId.IsEmpty)
        {
            return Result<TrainerTraineeLink, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (link == null)
        {
            return Result<TrainerTraineeLink, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        return Result<TrainerTraineeLink, AppError>.Success(link);
    }

    private static TrainerManagedPlanResult MapPlan(PlanEntity plan)
    {
        return new TrainerManagedPlanResult
        {
            Id = plan.Id,
            Name = plan.Name,
            IsActive = plan.IsActive,
            CreatedAt = plan.CreatedAt
        };
    }
}
