using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public sealed partial class TrainerRelationshipService
{
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
}
