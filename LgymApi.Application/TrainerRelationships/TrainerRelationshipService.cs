using LgymApi.Application.Exceptions;
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

    public async Task<TrainerInvitationResult> CreateInvitationAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);

        if (traineeId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (currentTrainer.Id == traineeId)
        {
            throw AppException.BadRequest(Messages.CannotInviteYourself);
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null || trainee.IsDeleted)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(traineeId, cancellationToken))
        {
            throw AppException.BadRequest(Messages.TraineeAlreadyLinked);
        }

        var existingPending = await _trainerRelationshipRepository.FindPendingInvitationAsync(currentTrainer.Id, traineeId, cancellationToken);
        var reusableInvitation = await HandleExistingPendingInvitationAsync(existingPending, cancellationToken);
        if (reusableInvitation != null)
        {
            return reusableInvitation;
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapInvitation(invitation);
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


    public async Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);

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

        return invitations.Select(MapInvitation).ToList();
    }

    public async Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);

        return await _trainerRelationshipRepository.GetDashboardTraineesAsync(currentTrainer.Id, query, cancellationToken);
    }

    public async Task<List<DateTime>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        return await _trainingService.GetTrainingDatesAsync(traineeId, cancellationToken);
    }

    public async Task<List<TrainingByDateDetails>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        return await _trainingService.GetTrainingByDateAsync(traineeId, createdAt, cancellationToken);
    }

    public async Task<List<ExerciseScoresChartData>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<ExerciseEntity> exerciseId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        return await _exerciseScoresService.GetExerciseScoresChartDataAsync(traineeId, exerciseId, cancellationToken);
    }

    public async Task<List<EloRegistryChartEntry>> GetTraineeEloChartAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        return await _eloRegistryService.GetChartAsync(traineeId, cancellationToken);
    }

    public async Task<List<MainRecordEntity>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        return await _mainRecordsService.GetMainRecordsHistoryAsync(traineeId, cancellationToken);
    }

    public async Task<List<TrainerManagedPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        var plans = await _planRepository.GetByUserIdAsync(traineeId, cancellationToken);

        return plans
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapPlan)
            .ToList();
    }

    public async Task<TrainerManagedPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, string name, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
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
        return MapPlan(plan);
    }

    public async Task<TrainerManagedPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);

        if (planId.IsEmpty || string.IsNullOrWhiteSpace(name))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null || plan.UserId != traineeId)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        plan.Name = name.Trim();
        await _planRepository.UpdateAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapPlan(plan);
    }

    public async Task DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);

        if (planId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null || plan.UserId != traineeId)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
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
    }

    public async Task AssignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);

        if (planId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = await _planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan == null || plan.UserId != traineeId)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _planRepository.SetActivePlanAsync(traineeId, planId, cancellationToken);
        trainee.PlanId = planId;
        await _userRepository.UpdateAsync(trainee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);

        var trainee = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)traineeId, cancellationToken);
        if (trainee == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _planRepository.ClearActivePlansAsync(traineeId, cancellationToken);
        trainee.PlanId = null;
        await _userRepository.UpdateAsync(trainee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<TrainerManagedPlanResult> GetActiveAssignedPlanAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var link = await _trainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(currentTrainee.Id, cancellationToken);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var activePlan = await _planRepository.FindActiveByUserIdAsync(currentTrainee.Id, cancellationToken);
        if (activePlan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return MapPlan(activePlan);
    }

    public async Task AcceptInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        if (invitationId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var invitation = await GetInvitationForTraineeAsync(currentTrainee, invitationId, cancellationToken);
        if (invitation.Status == TrainerInvitationStatus.Accepted)
        {
            var existing = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(invitation.TrainerId, currentTrainee.Id, cancellationToken);
            if (existing != null)
            {
                return;
            }
        }

        await EnsureInvitationPendingAsync(invitation, cancellationToken);

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(currentTrainee.Id, cancellationToken))
        {
            throw AppException.BadRequest(Messages.TraineeAlreadyLinked);
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
                throw AppException.BadRequest(Messages.TraineeAlreadyLinked);
            }

            throw;
        }
    }

    public async Task RejectInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
    {
        if (invitationId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var invitation = await GetInvitationForTraineeAsync(currentTrainee, invitationId, cancellationToken);
        if (invitation.Status == TrainerInvitationStatus.Rejected)
        {
            return;
        }

        await EnsureInvitationPendingAsync(invitation, cancellationToken);

        invitation.Status = TrainerInvitationStatus.Rejected;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlinkTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var link = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);

        await _trainerRelationshipRepository.RemoveLinkAsync(link, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DetachFromTrainerAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var link = await _trainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(currentTrainee.Id, cancellationToken);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _trainerRelationshipRepository.RemoveLinkAsync(link, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTrainerAsync(UserEntity currentTrainer, CancellationToken cancellationToken)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            throw AppException.Forbidden(Messages.TrainerRoleRequired);
        }
    }

    private async Task<TrainerTraineeLink> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken)
    {
        await EnsureTrainerAsync(currentTrainer, cancellationToken);

        if (traineeId.IsEmpty)
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return link;
    }

    private async Task<TrainerInvitation> GetInvitationForTraineeAsync(UserEntity currentTrainee, Id<TrainerInvitation> invitationId, CancellationToken cancellationToken)
    {
        var invitation = await _trainerRelationshipRepository.FindInvitationByIdAsync(invitationId, cancellationToken);
        if (invitation == null || invitation.TraineeId != currentTrainee.Id)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return invitation;
    }

    private async Task EnsureInvitationPendingAsync(TrainerInvitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            if (invitation.Status == TrainerInvitationStatus.Pending)
            {
                invitation.Status = TrainerInvitationStatus.Expired;
                invitation.RespondedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw AppException.BadRequest(Messages.InvitationExpired);
        }

        if (invitation.Status != TrainerInvitationStatus.Pending)
        {
            throw AppException.BadRequest(Messages.InvitationNoLongerPending);
        }
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
