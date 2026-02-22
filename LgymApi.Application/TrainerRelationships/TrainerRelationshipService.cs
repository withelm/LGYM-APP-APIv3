using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.MainRecords;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
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
    private readonly IInvitationEmailScheduler _invitationEmailScheduler;
    private readonly IEmailNotificationsFeature _emailNotificationsFeature;
    private readonly ITrainingService _trainingService;
    private readonly IExerciseScoresService _exerciseScoresService;
    private readonly IEloRegistryService _eloRegistryService;
    private readonly IMainRecordsService _mainRecordsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainerRelationshipService> _logger;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Service orchestrates trainer relationship aggregate and depends on dedicated domain services.")]
    public TrainerRelationshipService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        IPlanRepository planRepository,
        IInvitationEmailScheduler invitationEmailScheduler,
        IEmailNotificationsFeature emailNotificationsFeature,
        ITrainingService trainingService,
        IExerciseScoresService exerciseScoresService,
        IEloRegistryService eloRegistryService,
        IMainRecordsService mainRecordsService,
        IUnitOfWork unitOfWork,
        ILogger<TrainerRelationshipService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _trainerRelationshipRepository = trainerRelationshipRepository;
        _planRepository = planRepository;
        _invitationEmailScheduler = invitationEmailScheduler;
        _emailNotificationsFeature = emailNotificationsFeature;
        _trainingService = trainingService;
        _exerciseScoresService = exerciseScoresService;
        _eloRegistryService = eloRegistryService;
        _mainRecordsService = mainRecordsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TrainerInvitationResult> CreateInvitationAsync(UserEntity currentTrainer, Guid traineeId)
    {
        await EnsureTrainerAsync(currentTrainer);

        if (traineeId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (currentTrainer.Id == traineeId)
        {
            throw AppException.BadRequest(Messages.CannotInviteYourself);
        }

        var trainee = await _userRepository.FindByIdAsync(traineeId);
        if (trainee == null || trainee.IsDeleted)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(traineeId))
        {
            throw AppException.BadRequest(Messages.TraineeAlreadyLinked);
        }

        var existingPending = await _trainerRelationshipRepository.FindPendingInvitationAsync(currentTrainer.Id, traineeId);
        var reusableInvitation = await HandleExistingPendingInvitationAsync(existingPending);
        if (reusableInvitation != null)
        {
            return reusableInvitation;
        }

        var invitation = new TrainerInvitation
        {
            Id = Guid.NewGuid(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Code = CreateInvitationCode(),
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await _trainerRelationshipRepository.AddInvitationAsync(invitation);
        await _unitOfWork.SaveChangesAsync();

        await TryScheduleInvitationEmailAsync(currentTrainer, trainee, invitation);

        return MapInvitation(invitation);
    }

    private async Task<TrainerInvitationResult?> HandleExistingPendingInvitationAsync(TrainerInvitation? existingPending)
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
        await _unitOfWork.SaveChangesAsync();
        return null;
    }

    private async Task TryScheduleInvitationEmailAsync(UserEntity currentTrainer, UserEntity trainee, TrainerInvitation invitation)
    {
        if (!_emailNotificationsFeature.Enabled)
        {
            _logger.LogInformation(
                "Email notifications are disabled; invitation {InvitationId} created without scheduling email.",
                invitation.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(trainee.Email))
        {
            _logger.LogInformation(
                "Trainee email is empty; invitation {InvitationId} created without scheduling email.",
                invitation.Id);
            return;
        }

        try
        {
            await _invitationEmailScheduler.ScheduleInvitationCreatedAsync(new InvitationEmailPayload
            {
                InvitationId = invitation.Id,
                InvitationCode = invitation.Code,
                ExpiresAt = invitation.ExpiresAt,
                TrainerName = currentTrainer.Name,
                RecipientEmail = trainee.Email,
                CultureName = string.IsNullOrWhiteSpace(currentTrainer.PreferredLanguage)
                    ? "en-US"
                    : currentTrainer.PreferredLanguage
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to schedule invitation email for invitation {InvitationId}. Invitation creation is still successful.",
                invitation.Id);
        }
    }

    public async Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(UserEntity currentTrainer)
    {
        await EnsureTrainerAsync(currentTrainer);

        var invitations = await _trainerRelationshipRepository.GetInvitationsByTrainerIdAsync(currentTrainer.Id);
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
            await _unitOfWork.SaveChangesAsync();
        }

        return invitations.Select(MapInvitation).ToList();
    }

    public async Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query)
    {
        await EnsureTrainerAsync(currentTrainer);

        return await _trainerRelationshipRepository.GetDashboardTraineesAsync(currentTrainer.Id, query);
    }

    public async Task<List<DateTime>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Guid traineeId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);
        return await _trainingService.GetTrainingDatesAsync(traineeId);
    }

    public async Task<List<TrainingByDateDetails>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Guid traineeId, DateTime createdAt)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);
        return await _trainingService.GetTrainingByDateAsync(traineeId, createdAt);
    }

    public async Task<List<ExerciseScoresChartData>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Guid traineeId, Guid exerciseId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);
        return await _exerciseScoresService.GetExerciseScoresChartDataAsync(traineeId, exerciseId);
    }

    public async Task<List<EloRegistryChartEntry>> GetTraineeEloChartAsync(UserEntity currentTrainer, Guid traineeId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);
        return await _eloRegistryService.GetChartAsync(traineeId);
    }

    public async Task<List<MainRecordEntity>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Guid traineeId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);
        return await _mainRecordsService.GetMainRecordsHistoryAsync(traineeId);
    }

    public async Task<List<TrainerManagedPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Guid traineeId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);
        var plans = await _planRepository.GetByUserIdAsync(traineeId);

        return plans
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapPlan)
            .ToList();
    }

    public async Task<TrainerManagedPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, string name)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = new PlanEntity
        {
            Id = Guid.NewGuid(),
            UserId = traineeId,
            Name = name.Trim(),
            IsActive = false,
            IsDeleted = false
        };

        await _planRepository.AddAsync(plan);
        await _unitOfWork.SaveChangesAsync();
        return MapPlan(plan);
    }

    public async Task<TrainerManagedPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, string name)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);

        if (planId == Guid.Empty || string.IsNullOrWhiteSpace(name))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null || plan.UserId != traineeId)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        plan.Name = name.Trim();
        await _planRepository.UpdateAsync(plan);
        await _unitOfWork.SaveChangesAsync();
        return MapPlan(plan);
    }

    public async Task DeleteTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);

        if (planId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null || plan.UserId != traineeId)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var trainee = await _userRepository.FindByIdAsync(traineeId);
        if (trainee == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        plan.IsActive = false;
        plan.IsDeleted = true;
        await _planRepository.UpdateAsync(plan);

        if (trainee.PlanId == plan.Id)
        {
            trainee.PlanId = null;
            await _userRepository.UpdateAsync(trainee);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task AssignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);

        if (planId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = await _planRepository.FindByIdAsync(planId);
        if (plan == null || plan.UserId != traineeId)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var trainee = await _userRepository.FindByIdAsync(traineeId);
        if (trainee == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _planRepository.SetActivePlanAsync(traineeId, planId);
        trainee.PlanId = planId;
        await _userRepository.UpdateAsync(trainee);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UnassignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);

        var trainee = await _userRepository.FindByIdAsync(traineeId);
        if (trainee == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _planRepository.ClearActivePlansAsync(traineeId);
        trainee.PlanId = null;
        await _userRepository.UpdateAsync(trainee);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<TrainerManagedPlanResult> GetActiveAssignedPlanAsync(UserEntity currentTrainee)
    {
        var link = await _trainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(currentTrainee.Id);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var activePlan = await _planRepository.FindActiveByUserIdAsync(currentTrainee.Id);
        if (activePlan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return MapPlan(activePlan);
    }

    public async Task AcceptInvitationAsync(UserEntity currentTrainee, Guid invitationId)
    {
        if (invitationId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var invitation = await GetInvitationForTraineeAsync(currentTrainee, invitationId);
        if (invitation.Status == TrainerInvitationStatus.Accepted)
        {
            var existing = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(invitation.TrainerId, currentTrainee.Id);
            if (existing != null)
            {
                return;
            }
        }

        await EnsureInvitationPendingAsync(invitation);

        if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(currentTrainee.Id))
        {
            throw AppException.BadRequest(Messages.TraineeAlreadyLinked);
        }

        invitation.Status = TrainerInvitationStatus.Accepted;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        try
        {
            await _trainerRelationshipRepository.AddLinkAsync(new TrainerTraineeLink
            {
                Id = Guid.NewGuid(),
                TrainerId = invitation.TrainerId,
                TraineeId = currentTrainee.Id
            });

            await _unitOfWork.SaveChangesAsync();
        }
        catch
        {
            if (await _trainerRelationshipRepository.HasActiveLinkForTraineeAsync(currentTrainee.Id))
            {
                throw AppException.BadRequest(Messages.TraineeAlreadyLinked);
            }

            throw;
        }
    }

    public async Task RejectInvitationAsync(UserEntity currentTrainee, Guid invitationId)
    {
        if (invitationId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var invitation = await GetInvitationForTraineeAsync(currentTrainee, invitationId);
        if (invitation.Status == TrainerInvitationStatus.Rejected)
        {
            return;
        }

        await EnsureInvitationPendingAsync(invitation);

        invitation.Status = TrainerInvitationStatus.Rejected;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UnlinkTraineeAsync(UserEntity currentTrainer, Guid traineeId)
    {
        var link = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId);

        await _trainerRelationshipRepository.RemoveLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DetachFromTrainerAsync(UserEntity currentTrainee)
    {
        var link = await _trainerRelationshipRepository.FindActiveLinkByTraineeIdAsync(currentTrainee.Id);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        await _trainerRelationshipRepository.RemoveLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureTrainerAsync(UserEntity currentTrainer)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer);
        if (!isTrainer)
        {
            throw AppException.Forbidden(Messages.TrainerRoleRequired);
        }
    }

    private async Task<TrainerTraineeLink> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Guid traineeId)
    {
        await EnsureTrainerAsync(currentTrainer);

        if (traineeId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return link;
    }

    private async Task<TrainerInvitation> GetInvitationForTraineeAsync(UserEntity currentTrainee, Guid invitationId)
    {
        var invitation = await _trainerRelationshipRepository.FindInvitationByIdAsync(invitationId);
        if (invitation == null || invitation.TraineeId != currentTrainee.Id)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return invitation;
    }

    private async Task EnsureInvitationPendingAsync(TrainerInvitation invitation)
    {
        if (invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            if (invitation.Status == TrainerInvitationStatus.Pending)
            {
                invitation.Status = TrainerInvitationStatus.Expired;
                invitation.RespondedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync();
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
        return Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    }

}
