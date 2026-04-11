using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Supplementation;

public sealed partial class SupplementationService
{
    public async Task<Result<List<SupplementPlanResult>, AppError>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<SupplementPlanResult>, AppError>.Failure(ensureResult.Error);
        }

        var plans = await _supplementationRepository.GetPlansByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return Result<List<SupplementPlanResult>, AppError>.Success(plans.Select(MapPlan).ToList());
    }

    public async Task<Result<SupplementPlanResult, AppError>> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<SupplementPlanResult, AppError>.Failure(ensureResult.Error);
        }

        var validationResult = ValidateAndNormalizeItems(command);
        if (validationResult.IsFailure)
        {
            return Result<SupplementPlanResult, AppError>.Failure(validationResult.Error);
        }

        var normalizedItems = validationResult.Value;

        var plan = new SupplementPlan
        {
            Id = Id<SupplementPlan>.New(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Name = command.Name.Trim(),
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            IsActive = false,
            IsDeleted = false,
            Items = normalizedItems.Select(item => new SupplementPlanItem
            {
                Id = Id<SupplementPlanItem>.New(),
                SupplementName = item.SupplementName,
                Dosage = item.Dosage,
                TimeOfDay = item.TimeOfDay,
                DaysOfWeekMask = (DaysOfWeekSet)item.DaysOfWeekMask,
                Order = item.Order
            }).ToList()
        };

        await _supplementationRepository.AddPlanAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SupplementPlanResult, AppError>.Success(MapPlan(plan));
    }

    public async Task<Result<SupplementPlanResult, AppError>> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<SupplementPlanResult, AppError>.Failure(ensureResult.Error);
        }

        var validationResult = ValidateAndNormalizeItems(command);
        if (validationResult.IsFailure)
        {
            return Result<SupplementPlanResult, AppError>.Failure(validationResult.Error);
        }

        var normalizedItems = validationResult.Value;

        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, planId, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<SupplementPlanResult, AppError>.Failure(planResult.Error);
        }

        var plan = planResult.Value;
        var wasActive = plan.IsActive;

        plan.IsDeleted = true;
        plan.IsActive = false;

        var newPlan = new SupplementPlan
        {
            Id = Id<SupplementPlan>.New(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Name = command.Name.Trim(),
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            IsActive = wasActive,
            IsDeleted = false,
            Items = normalizedItems.Select(item => new SupplementPlanItem
            {
                Id = Id<SupplementPlanItem>.New(),
                SupplementName = item.SupplementName,
                Dosage = item.Dosage,
                TimeOfDay = item.TimeOfDay,
                DaysOfWeekMask = (DaysOfWeekSet)item.DaysOfWeekMask,
                Order = item.Order
            }).ToList()
        };

        await _supplementationRepository.AddPlanAsync(newPlan, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SupplementPlanResult, AppError>.Success(MapPlan(newPlan));
    }

    public async Task<Result<Unit, AppError>> DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return ensureResult;
        }

        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, planId, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(planResult.Error);
        }

        var plan = planResult.Value;
        plan.IsDeleted = true;
        plan.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> AssignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return ensureResult;
        }

        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, planId, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(planResult.Error);
        }

        var plan = planResult.Value;

        var existingPlans = await _supplementationRepository.GetPlansByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        foreach (var candidate in existingPlans.Where(x => x.IsActive && x.Id != plan.Id))
        {
            candidate.IsActive = false;
        }

        plan.IsActive = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UnassignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return ensureResult;
        }

        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(traineeId, cancellationToken);
        if (activePlan == null || activePlan.TrainerId != currentTrainer.Id)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        activePlan.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
