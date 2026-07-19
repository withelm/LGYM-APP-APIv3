using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Nutrition.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;
namespace LgymApi.Application.Features.DietPlans;

public sealed class DietPlanService : IDietPlanService
{
    private readonly IDietPlanRepository _dietPlanRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public DietPlanService(
        IDietPlanRepository dietPlanRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        ICommandDispatcher commandDispatcher,
        IUnitOfWork unitOfWork)
    {
        _dietPlanRepository = dietPlanRepository;
        _trainerRelationshipRepository = trainerRelationshipRepository;
        _commandDispatcher = commandDispatcher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<DietPlanResult>, AppError>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<List<DietPlanResult>, AppError>.Failure(ownership.Error);
        }

        var plans = await _dietPlanRepository.GetPlansByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return Result<List<DietPlanResult>, AppError>.Success(plans.Select(DietPlanMapping.MapPlan).ToList());
    }

    public async Task<Result<DietPlanResult, AppError>> GetTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default)
    {
        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, dietPlanId, cancellationToken);
        return planResult.IsFailure
            ? Result<DietPlanResult, AppError>.Failure(planResult.Error)
            : Result<DietPlanResult, AppError>.Success(DietPlanMapping.MapPlan(planResult.Value));
    }

    public async Task<Result<DietPlanResult, AppError>> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, UpsertDietPlanCommand command, CancellationToken cancellationToken = default)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<DietPlanResult, AppError>.Failure(ownership.Error);
        }

        var shapeValidation = ValidatePlanShape(command);
        if (shapeValidation.IsFailure)
        {
            return Result<DietPlanResult, AppError>.Failure(shapeValidation.Error);
        }

        var normalizedMeals = NormalizeMeals(command.Meals, command);
        if (normalizedMeals.IsFailure)
        {
            return Result<DietPlanResult, AppError>.Failure(normalizedMeals.Error);
        }

        var plan = new DietPlan
        {
            Id = Id<DietPlan>.New(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Name = command.Name.Trim(),
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            EstimatedCalories = command.EstimatedCalories,
            ProteinGrams = command.ProteinGrams,
            CarbsGrams = command.CarbsGrams,
            FatGrams = command.FatGrams,
            Notes = NormalizeNullable(command.Notes),
            IsActive = command.IsActive,
            IsDeleted = false,
            Meals = DietPlanMapping.BuildMeals(normalizedMeals.Value)
        };

        await _dietPlanRepository.AddPlanAsync(plan, cancellationToken);
        await AddHistoryEntryAsync(plan, currentTrainer.Id, "Created", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (plan.IsActive)
        {
            await NotifyDietPlanUpdatedAsync(plan, currentTrainer.Id);
        }

        return Result<DietPlanResult, AppError>.Success(DietPlanMapping.MapPlan(plan));
    }

    public async Task<Result<DietPlanResult, AppError>> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, UpsertDietPlanCommand command, CancellationToken cancellationToken = default)
    {
        var shapeValidation = ValidatePlanShape(command);
        if (shapeValidation.IsFailure)
        {
            return Result<DietPlanResult, AppError>.Failure(shapeValidation.Error);
        }

        var normalizedMeals = NormalizeMeals(command.Meals, command);
        if (normalizedMeals.IsFailure)
        {
            return Result<DietPlanResult, AppError>.Failure(normalizedMeals.Error);
        }

        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, dietPlanId, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<DietPlanResult, AppError>.Failure(planResult.Error);
        }

        var plan = planResult.Value;
        plan.Name = command.Name.Trim();
        plan.StartDate = command.StartDate;
        plan.EndDate = command.EndDate;
        plan.EstimatedCalories = command.EstimatedCalories;
        plan.ProteinGrams = command.ProteinGrams;
        plan.CarbsGrams = command.CarbsGrams;
        plan.FatGrams = command.FatGrams;
        plan.Notes = NormalizeNullable(command.Notes);
        plan.IsActive = command.IsActive;

        DietPlanMapping.ReplaceMeals(plan, normalizedMeals.Value);

        await AddHistoryEntryAsync(plan, currentTrainer.Id, "Updated", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (plan.IsActive)
        {
            await NotifyDietPlanUpdatedAsync(plan, currentTrainer.Id);
        }

        return Result<DietPlanResult, AppError>.Success(DietPlanMapping.MapPlan(plan));
    }

    public async Task<Result<Unit, AppError>> ActivateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default)
    {
        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, dietPlanId, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(planResult.Error);
        }

        var plan = planResult.Value;
        plan.IsActive = true;
        await AddHistoryEntryAsync(plan, currentTrainer.Id, "Activated", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyDietPlanUpdatedAsync(plan, currentTrainer.Id);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default)
    {
        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, dietPlanId, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<Unit, AppError>.Failure(planResult.Error);
        }

        var plan = planResult.Value;
        plan.IsDeleted = true;
        plan.IsActive = false;
        await AddHistoryEntryAsync(plan, currentTrainer.Id, "Deleted", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<List<DietPlanHistoryResult>, AppError>> GetTraineePlanHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default)
    {
        var planResult = await EnsureOwnedPlanAsync(currentTrainer, traineeId, dietPlanId, cancellationToken);
        if (planResult.IsFailure)
        {
            return Result<List<DietPlanHistoryResult>, AppError>.Failure(planResult.Error);
        }

        var history = await _dietPlanRepository.GetPlanHistoryAsync(planResult.Value.Id, cancellationToken);
        return Result<List<DietPlanHistoryResult>, AppError>.Success(history.Select(DietPlanMapping.MapHistory).ToList());
    }

    public async Task<Result<List<DietPlanResult>, AppError>> GetCurrentPlansAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var plans = await _dietPlanRepository.GetActivePlansForTraineeAsync(currentTrainee.Id, cancellationToken);
        return Result<List<DietPlanResult>, AppError>.Success(plans.Select(DietPlanMapping.MapPlan).ToList());
    }

    public async Task<Result<DietPlanResult, AppError>> GetCurrentPlanAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default)
    {
        var plan = await _dietPlanRepository.GetActivePlanForTraineeAsync(currentTrainee.Id, cancellationToken);
        return plan == null
            ? Result<DietPlanResult, AppError>.Failure(new NotFoundError(Messages.DidntFind))
            : Result<DietPlanResult, AppError>.Success(DietPlanMapping.MapPlan(plan));
    }

    private async Task<Result<Unit, AppError>> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken)
    {
        if (traineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new BadRequestError(Messages.UserIdRequired));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return link == null
            ? Result<Unit, AppError>.Failure(new NotFoundError(Messages.DidntFind))
            : Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<DietPlan, AppError>> EnsureOwnedPlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken)
    {
        var ownership = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result<DietPlan, AppError>.Failure(ownership.Error);
        }

        if (dietPlanId.IsEmpty)
        {
            return Result<DietPlan, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        var plan = await _dietPlanRepository.FindPlanByIdAsync(dietPlanId, cancellationToken);
        return plan == null || plan.TrainerId != currentTrainer.Id || plan.TraineeId != traineeId || plan.IsDeleted
            ? Result<DietPlan, AppError>.Failure(new NotFoundError(Messages.DidntFind))
            : Result<DietPlan, AppError>.Success(plan);
    }

    private static Result<List<UpsertDietMealCommand>, AppError> NormalizeMeals(List<UpsertDietMealCommand>? meals, UpsertDietPlanCommand command)
    {
        if (meals == null || meals.Count == 0)
        {
            return HasAnyDietTargets(command)
                ? Result<List<UpsertDietMealCommand>, AppError>.Success([])
                : Result<List<UpsertDietMealCommand>, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        var normalized = meals
            .Select((meal, index) => new UpsertDietMealCommand
            {
                Name = meal.Name.Trim(),
                Order = meal.Order < 0 ? index : meal.Order,
                Description = NormalizeNullable(meal.Description),
                EstimatedCalories = meal.EstimatedCalories,
                ProteinGrams = meal.ProteinGrams,
                CarbsGrams = meal.CarbsGrams,
                FatGrams = meal.FatGrams
            })
            .OrderBy(x => x.Order)
            .ToList();

        return normalized.Any(x => string.IsNullOrWhiteSpace(x.Name))
            ? Result<List<UpsertDietMealCommand>, AppError>.Failure(new BadRequestError(Messages.FieldRequired))
            : Result<List<UpsertDietMealCommand>, AppError>.Success(normalized);
    }

    private static bool HasAnyDietTargets(UpsertDietPlanCommand command)
        => command.EstimatedCalories.HasValue
           || command.ProteinGrams.HasValue
           || command.CarbsGrams.HasValue
           || command.FatGrams.HasValue;

    private static Result<Unit, AppError> ValidatePlanShape(UpsertDietPlanCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<Unit, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        if (command.StartDate == default)
        {
            return Result<Unit, AppError>.Failure(new BadRequestError(Messages.FieldRequired));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private Task AddHistoryEntryAsync(DietPlan plan, Id<UserEntity> changedByUserId, string changeType, CancellationToken cancellationToken)
        => _dietPlanRepository.AddHistoryEntryAsync(DietPlanMapping.CreateHistoryEntry(plan, changedByUserId, changeType), cancellationToken);

    private Task NotifyDietPlanUpdatedAsync(DietPlan plan, Id<UserEntity> trainerId)
        => _commandDispatcher.EnqueueAsync(new DietPlanUpdatedInAppNotificationCommand
        {
            DietPlanId = plan.Id,
            TraineeId = plan.TraineeId,
            TrainerId = trainerId,
            DietPlanName = plan.Name,
            TriggeredAt = DateTimeOffset.UtcNow
        });

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
