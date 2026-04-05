using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Features.Training.Models;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public sealed class TrainerRelationshipService : ITrainerRelationshipService
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

    public async Task<Result<TrainerInvitationResult, AppError>> CreateInvitationAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(ensureTrainerResult.Error);
        }

        if (traineeId.IsEmpty)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired));
        }

        if (currentTrainer.Id == traineeId)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.CannotInviteYourself));
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null || trainee.IsDeleted)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(traineeId, cancellationToken))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.TraineeAlreadyLinked));
        }

        var existingPending = await _trainerRelationshipRepository.FindPendingInvitationAsync(currentTrainer.Id, traineeId, cancellationToken);
        var reusableInvitation = await HandleExistingPendingInvitationAsync(existingPending, cancellationToken);
        if (reusableInvitation != null)
        {
            return Result<TrainerInvitationResult, AppError>.Success(reusableInvitation);
        }

        var invitation = new TrainerInvitation
        {
            Id = Id<TrainerInvitation>.New(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Code = CreateInvitationCode(),
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await _trainerRelationshipRepository.AddInvitationAsync(invitation, cancellationToken);
        await _commandDispatcher.EnqueueAsync(new InvitationCreatedCommand { InvitationId = invitation.Id });
        await _commandDispatcher.EnqueueAsync(new TrainerInvitationCreatedInAppNotificationCommand
        {
            TraineeId = traineeId,
            TrainerId = currentTrainer.Id
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TrainerInvitationResult, AppError>.Success(MapInvitation(invitation));
    }

    public async Task<Result<TrainerInvitationResult, AppError>> CreateInvitationByEmailAsync(
        UserEntity currentTrainer,
        string inviteeEmail,
        string preferredLanguage,
        string preferredTimeZone,
        CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(ensureTrainerResult.Error);
        }

        var normalizedInviteeEmail = new Email(inviteeEmail).Value;
        if (string.Equals(currentTrainer.Email.Value, normalizedInviteeEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.CannotInviteYourself));
        }

        var existingPending = await _trainerRelationshipRepository.FindPendingInvitationByEmailAsync(currentTrainer.Id, normalizedInviteeEmail, cancellationToken);
        if (existingPending != null)
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new TrainerRelationshipConflictError(Messages.InvitationPendingForEmail));
        }

        if (await _trainerRelationshipRepository.IsEmailAlreadyTraineeAsync(currentTrainer.Id, normalizedInviteeEmail, cancellationToken))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new TrainerRelationshipConflictError(Messages.EmailAlreadyYourTrainee));
        }

        var trainee = await _userRepository.FindByEmailAsync(normalizedInviteeEmail, cancellationToken);

        var invitation = new TrainerInvitation
        {
            Id = Id<TrainerInvitation>.New(),
            TrainerId = currentTrainer.Id,
            InviteeEmail = normalizedInviteeEmail,
            TraineeId = trainee?.Id,
            Code = CreateInvitationCode(),
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await _trainerRelationshipRepository.AddInvitationAsync(invitation, cancellationToken);
        await _commandDispatcher.EnqueueAsync(new InvitationCreatedCommand { InvitationId = invitation.Id });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TrainerInvitationResult, AppError>.Success(MapInvitation(invitation));
    }

    private async Task<TrainerInvitationResult?> HandleExistingPendingInvitationAsync(TrainerInvitation? existingPending, CancellationToken cancellationToken)
    {
        if (existingPending == null)
        {
            return null;
        }

        if (existingPending.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return MapInvitation(existingPending);
        }

        existingPending.Status = TrainerInvitationStatus.Expired;
        existingPending.RespondedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return null;
    }


    public async Task<Result<List<TrainerInvitationResult>, AppError>> GetTrainerInvitationsAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<List<TrainerInvitationResult>, AppError>.Failure(ensureTrainerResult.Error);
        }

        var invitations = await _trainerRelationshipRepository.GetInvitationsByTrainerIdAsync(currentTrainer.Id, cancellationToken);
        var hasUpdates = false;

        foreach (var invitation in invitations)
        {
            if (invitation.Status == TrainerInvitationStatus.Pending && invitation.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                invitation.Status = TrainerInvitationStatus.Expired;
                invitation.RespondedAt = DateTimeOffset.UtcNow;
                hasUpdates = true;
            }
        }

        if (hasUpdates)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<List<TrainerInvitationResult>, AppError>.Success(invitations.Select(MapInvitation).ToList());
    }

    public async Task<Result<TrainerDashboardTraineeListResult, AppError>> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<TrainerDashboardTraineeListResult, AppError>.Failure(ensureTrainerResult.Error);
        }

        return Result<TrainerDashboardTraineeListResult, AppError>.Success(
            await _trainerRelationshipRepository.GetDashboardTraineesAsync(currentTrainer.Id, query, cancellationToken));
    }

    public async Task<Result<List<DateTime>, AppError>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<DateTime>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _trainingService.GetTrainingDatesAsync(traineeId, cancellationToken);
 
        if (result.IsFailure)
        {
            return Result<List<DateTime>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<DateTime>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<TrainingByDateDetails>, AppError>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _trainingService.GetTrainingByDateAsync(traineeId, createdAt, cancellationToken);

        if (result.IsFailure)
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<TrainingByDateDetails>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<ExerciseScoresChartData>, AppError>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<ExerciseEntity> exerciseId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _exerciseScoresService.GetExerciseScoresChartDataAsync(traineeId, exerciseId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<ExerciseScoresChartData>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<EloRegistryChartEntry>, AppError>> GetTraineeEloChartAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _eloRegistryService.GetChartAsync(traineeId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<EloRegistryChartEntry>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<MainRecordEntity>, AppError>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<MainRecordEntity>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _mainRecordsService.GetMainRecordsHistoryAsync(traineeId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<MainRecordEntity>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<MainRecordEntity>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<TrainerManagedPlanResult>, AppError>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<TrainerManagedPlanResult>, AppError>.Failure(ensureResult.Error);
        }

        var plans = await _planRepository.GetByUserIdAsync(traineeId, cancellationToken);
        var mapped = plans.OrderByDescending(x => x.CreatedAt).Select(MapPlan).ToList();
        return Result<List<TrainerManagedPlanResult>, AppError>.Success(mapped);
    }

    public async Task<Result<TrainerManagedPlanResult, AppError>> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, string name, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(ensureResult.Error);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var plan = new PlanEntity
        {
            Id = Id<PlanEntity>.New(),
            UserId = traineeId,
            Name = name.Trim(),
            IsActive = false,
            IsDeleted = false
        };

        await _planRepository.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TrainerManagedPlanResult, AppError>.Success(MapPlan(plan));
    }

    public async Task<Result<TrainerManagedPlanResult, AppError>> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(ensureResult.Error);
        }

        if (planId.IsEmpty || string.IsNullOrWhiteSpace(name))
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null || plan.UserId != traineeId)
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        plan.Name = name.Trim();
        await _planRepository.UpdateAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<TrainerManagedPlanResult, AppError>.Success(MapPlan(plan));
    }

    public async Task<Result<Unit, AppError>> DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(ensureResult.Error);
        }

        if (planId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null || plan.UserId != traineeId)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        plan.IsActive = false;
        plan.IsDeleted = true;
        await _planRepository.UpdateAsync(plan, cancellationToken);

        if (trainee.PlanId == plan.Id)
        {
            trainee.PlanId = null;
            await _userRepository.UpdateAsync(trainee, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> AssignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(ensureResult.Error);
        }

        if (planId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null || plan.UserId != traineeId)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        await _planRepository.SetActivePlanAsync(traineeId, planId, cancellationToken);
        trainee.PlanId = planId;
        await _userRepository.UpdateAsync(trainee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UnassignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(ensureResult.Error);
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        await _planRepository.ClearActivePlansAsync(traineeId, cancellationToken);
        trainee.PlanId = null;
        await _userRepository.UpdateAsync(trainee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<TrainerManagedPlanResult, AppError>> GetActiveAssignedPlanAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var link = await _trainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(currentTrainee.Id, cancellationToken);
        if (link == null)
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        var activePlan = await _planRepository.FindActiveByUserIdAsync(currentTrainee.Id, cancellationToken);
        if (activePlan == null)
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        return Result<TrainerManagedPlanResult, AppError>.Success(MapPlan(activePlan));
    }

    public async Task<Result<Unit, AppError>> AcceptInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        if (invitationId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var invitationResult = await GetInvitationForTraineeAsync(currentTrainee, invitationId, cancellationToken);
        if (invitationResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(invitationResult.Error);
        }

        var invitation = invitationResult.Value;
        if (invitation.Status == TrainerInvitationStatus.Accepted)
        {
            var existing = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(invitation.TrainerId, currentTrainee.Id, cancellationToken);
            if (existing != null)
            {
                return Result<Unit, AppError>.Success(Unit.Value);
            }
        }

        var invitationPendingResult = await EnsureInvitationPendingAsync(invitation, cancellationToken);
        if (invitationPendingResult.IsFailure)
        {
            return invitationPendingResult;
        }

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(currentTrainee.Id, cancellationToken))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.TraineeAlreadyLinked));
        }

        invitation.Status = TrainerInvitationStatus.Accepted;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        try
        {
            await _trainerRelationshipRepository.AddLinkAsync(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(),
                TrainerId = invitation.TrainerId,
                TraineeId = currentTrainee.Id
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(currentTrainee.Id, cancellationToken))
            {
                return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.TraineeAlreadyLinked));
            }

            throw;
        }

        await _commandDispatcher.EnqueueAsync(new InvitationAcceptedCommand { InvitationId = invitation.Id });

        await _commandDispatcher.EnqueueAsync(new TrainerInvitationAcceptedInAppNotificationCommand
        {
            TrainerId = invitation.TrainerId,
            TraineeId = currentTrainee.Id
        });

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> RejectInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        if (invitationId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var invitationResult = await GetInvitationForTraineeAsync(currentTrainee, invitationId, cancellationToken);
        if (invitationResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(invitationResult.Error);
        }

        var invitation = invitationResult.Value;
        if (invitation.Status == TrainerInvitationStatus.Rejected)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        var invitationPendingResult = await EnsureInvitationPendingAsync(invitation, cancellationToken);
        if (invitationPendingResult.IsFailure)
        {
            return invitationPendingResult;
        }

        invitation.Status = TrainerInvitationStatus.Rejected;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _commandDispatcher.EnqueueAsync(new TrainerInvitationRejectedInAppNotificationCommand
        {
            TrainerId = invitation.TrainerId,
            TraineeId = currentTrainee.Id
        });
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> RevokeInvitationAsync(UserEntity currentTrainer, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(ensureTrainerResult.Error);
        }

        if (invitationId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired));
        }

        var invitation = await _trainerRelationshipRepository.FindInvitationByIdAsync(invitationId, cancellationToken);
        if (invitation == null || invitation.TrainerId != currentTrainer.Id)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        if (invitation.Status != TrainerInvitationStatus.Pending)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.InvitationNoLongerPending));
        }

        invitation.Status = TrainerInvitationStatus.Revoked;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _commandDispatcher.EnqueueAsync(new InvitationRevokedCommand { InvitationId = invitation.Id });

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UnlinkTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var linkResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (linkResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(linkResult.Error);
        }

        await _trainerRelationshipRepository.RemoveLinkAsync(linkResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> DetachFromTrainerAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var link = await _trainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(currentTrainee.Id, cancellationToken);
        if (link == null)
        {
            return Result<Unit, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        await _trainerRelationshipRepository.RemoveLinkAsync(link, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
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

    private async Task<Result<TrainerInvitation, AppError>> GetInvitationForTraineeAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken)
    {
        var invitation = await _trainerRelationshipRepository.FindInvitationByIdAsync(invitationId, cancellationToken);
        if (invitation == null)
        {
            return Result<TrainerInvitation, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        if (invitation.TraineeId == null)
        {
            if (!string.Equals(invitation.InviteeEmail, currentTrainee.Email.Value, StringComparison.OrdinalIgnoreCase))
            {
                return Result<TrainerInvitation, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
            }

            invitation.TraineeId = currentTrainee.Id;
        }
        else if (invitation.TraineeId != currentTrainee.Id)
        {
            return Result<TrainerInvitation, AppError>.Failure(new TrainerRelationshipNotFoundError(Messages.DidntFind));
        }

        return Result<TrainerInvitation, AppError>.Success(invitation);
    }

    private async Task<Result<Unit, AppError>> EnsureInvitationPendingAsync(TrainerInvitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            if (invitation.Status == TrainerInvitationStatus.Pending)
            {
                invitation.Status = TrainerInvitationStatus.Expired;
                invitation.RespondedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.InvitationExpired));
        }

        if (invitation.Status != TrainerInvitationStatus.Pending)
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.InvitationNoLongerPending));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static TrainerInvitationResult MapInvitation(TrainerInvitation invitation)
    {
        return new TrainerInvitationResult
        {
            Id = invitation.Id,
            TrainerId = invitation.TrainerId,
            TraineeId = invitation.TraineeId,
            Code = invitation.Code,
            Status = invitation.Status,
            ExpiresAt = invitation.ExpiresAt,
            RespondedAt = invitation.RespondedAt,
            CreatedAt = invitation.CreatedAt
        };
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

    private static string CreateInvitationCode()
    {
        return Id<TrainerInvitation>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal)[..12].ToUpperInvariant();
    }

}
