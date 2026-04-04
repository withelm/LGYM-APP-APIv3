using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Supplementation;

public sealed class SupplementationService : ISupplementationService
{
    private const int MaxComplianceRangeDays = 366;
    private const int PlanNameMaxLength = 120;
    private const int PlanNotesMaxLength = 1000;
    private const int SupplementNameMaxLength = 160;
    private const int DosageMaxLength = 120;

    private readonly IRoleRepository _roleRepository;
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly ISupplementationRepository _supplementationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SupplementationService(
        IRoleRepository roleRepository,
        ITrainerRelationshipRepository trainerRelationshipRepository,
        ISupplementationRepository supplementationRepository,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _trainerRelationshipRepository = trainerRelationshipRepository;
        _supplementationRepository = supplementationRepository;
        _unitOfWork = unitOfWork;
    }

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

    public async Task<Result<List<SupplementScheduleEntryResult>, AppError>> GetActiveScheduleForDateAsync(UserEntity currentTrainee, DateOnly date, CancellationToken cancellationToken = default)
    {
        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(currentTrainee.Id, cancellationToken);
        if (activePlan == null)
        {
            return Result<List<SupplementScheduleEntryResult>, AppError>.Success([]);
        }

        var logs = await _supplementationRepository.GetIntakeLogsForPlanAsync(currentTrainee.Id, activePlan.Id, date, date, cancellationToken);
        var logsByPlanItem = logs.ToDictionary(x => x.PlanItemId, x => x);

        var schedule = activePlan.Items
            .Where(item => IsScheduledOnDate(item.DaysOfWeekMask, date))
            .OrderBy(item => item.Order)
            .ThenBy(item => item.TimeOfDay)
            .ThenBy(item => item.CreatedAt)
            .Select(item =>
            {
                var hasLog = logsByPlanItem.TryGetValue(item.Id, out var log);
                return new SupplementScheduleEntryResult
                {
                    PlanItemId = item.Id,
                    SupplementName = item.SupplementName,
                    Dosage = item.Dosage,
                    TimeOfDay = FormatTime(item.TimeOfDay),
                    IntakeDate = date,
                    Taken = hasLog,
                    TakenAt = hasLog ? log!.TakenAt : null
                };
            })
            .ToList();

        return Result<List<SupplementScheduleEntryResult>, AppError>.Success(schedule);
    }

    public async Task<Result<SupplementScheduleEntryResult, AppError>> CheckOffIntakeAsync(UserEntity currentTrainee, CheckOffSupplementIntakeCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PlanItemId.IsEmpty)
        {
            return Result<SupplementScheduleEntryResult, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
        }

        if (command.IntakeDate == default)
        {
            return Result<SupplementScheduleEntryResult, AppError>.Failure(new InvalidSupplementationError(Messages.DateRequired));
        }

        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(currentTrainee.Id, cancellationToken);
        if (activePlan == null)
        {
            return Result<SupplementScheduleEntryResult, AppError>.Failure(new SupplementationNotFoundError(Messages.DidntFind));
        }

        var planItem = activePlan.Items.FirstOrDefault(item => item.Id == command.PlanItemId);
        if (planItem == null || !IsScheduledOnDate(planItem.DaysOfWeekMask, command.IntakeDate))
        {
            return Result<SupplementScheduleEntryResult, AppError>.Failure(new SupplementationNotFoundError(Messages.DidntFind));
        }

        var existing = await _supplementationRepository.FindIntakeLogAsync(currentTrainee.Id, command.PlanItemId, command.IntakeDate, cancellationToken);
        if (existing == null)
        {
            existing = new SupplementIntakeLog
            {
                Id = Id<SupplementIntakeLog>.New(),
                TraineeId = currentTrainee.Id,
                PlanItemId = command.PlanItemId,
                IntakeDate = command.IntakeDate,
                TakenAt = command.TakenAt ?? DateTimeOffset.UtcNow
            };

            await _supplementationRepository.AddIntakeLogAsync(existing, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                var persisted = await _supplementationRepository.FindIntakeLogAsync(currentTrainee.Id, command.PlanItemId, command.IntakeDate, cancellationToken);
                if (persisted == null)
                {
                    throw;
                }

                existing = persisted;
            }
        }
        else
        {
            existing.TakenAt = command.TakenAt ?? existing.TakenAt;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var result = new SupplementScheduleEntryResult
        {
            PlanItemId = planItem.Id,
            SupplementName = planItem.SupplementName,
            Dosage = planItem.Dosage,
            TimeOfDay = FormatTime(planItem.TimeOfDay),
            IntakeDate = command.IntakeDate,
            Taken = true,
            TakenAt = existing.TakenAt
        };

        return Result<SupplementScheduleEntryResult, AppError>.Success(result);
    }

    public async Task<Result<SupplementComplianceSummaryResult, AppError>> GetComplianceSummaryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<SupplementComplianceSummaryResult, AppError>.Failure(ensureResult.Error);
        }

        if (toDate < fromDate)
        {
            return Result<SupplementComplianceSummaryResult, AppError>.Failure(new InvalidSupplementationError(Messages.InvalidDateRange));
        }

        var inclusiveRangeDays = toDate.DayNumber - fromDate.DayNumber + 1;
        if (inclusiveRangeDays > MaxComplianceRangeDays)
        {
            return Result<SupplementComplianceSummaryResult, AppError>.Failure(new InvalidSupplementationError(Messages.DateRangeTooLarge));
        }

        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(traineeId, cancellationToken);
        if (activePlan == null || activePlan.TrainerId != currentTrainer.Id)
        {
            var emptyResult = new SupplementComplianceSummaryResult
            {
                TraineeId = traineeId,
                FromDate = fromDate,
                ToDate = toDate,
                PlannedDoses = 0,
                TakenDoses = 0,
                AdherenceRate = 0
            };
            return Result<SupplementComplianceSummaryResult, AppError>.Success(emptyResult);
        }

        var plannedDoses = 0;
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            plannedDoses += activePlan.Items.Count(item => IsScheduledOnDate(item.DaysOfWeekMask, date));
        }

        var logs = await _supplementationRepository.GetIntakeLogsForPlanAsync(traineeId, activePlan.Id, fromDate, toDate, cancellationToken);
        var takenDoses = logs.Count;
        var adherenceRate = plannedDoses == 0 ? 0 : Math.Round((double)takenDoses / plannedDoses * 100, 2);

        var result = new SupplementComplianceSummaryResult
        {
            TraineeId = traineeId,
            FromDate = fromDate,
            ToDate = toDate,
            PlannedDoses = plannedDoses,
            TakenDoses = takenDoses,
            AdherenceRate = adherenceRate
        };

        return Result<SupplementComplianceSummaryResult, AppError>.Success(result);
    }

    private async Task<Result<Unit, AppError>> EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            return Result<Unit, AppError>.Failure(new SupplementationForbiddenError(Messages.TrainerRoleRequired));
        }

        if (traineeId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidSupplementationError(Messages.UserIdRequired));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (link == null)
        {
            return Result<Unit, AppError>.Failure(new SupplementationNotFoundError(Messages.DidntFind));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task<Result<SupplementPlan, AppError>> EnsureOwnedPlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, CancellationToken cancellationToken)
    {
        if (planId.IsEmpty)
        {
            return Result<SupplementPlan, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
        }

        var plan = await _supplementationRepository.FindPlanByIdAsync(planId, cancellationToken);
        if (plan == null
            || plan.TrainerId != currentTrainer.Id
            || plan.TraineeId != traineeId
            || plan.IsDeleted)
        {
            return Result<SupplementPlan, AppError>.Failure(new SupplementationNotFoundError(Messages.DidntFind));
        }

        return Result<SupplementPlan, AppError>.Success(plan);
    }

    private static Result<List<NormalizedPlanItem>, AppError> ValidateAndNormalizeItems(UpsertSupplementPlanCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name)
            || command.Name.Trim().Length > PlanNameMaxLength
            || command.Items.Count == 0)
        {
            return Result<List<NormalizedPlanItem>, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
        }

        if (command.Notes?.Trim().Length > PlanNotesMaxLength)
        {
            return Result<List<NormalizedPlanItem>, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
        }

        var normalizedItems = new List<NormalizedPlanItem>(command.Items.Count);
        foreach (var item in command.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SupplementName)
                || string.IsNullOrWhiteSpace(item.Dosage)
                || string.IsNullOrWhiteSpace(item.TimeOfDay))
            {
                return Result<List<NormalizedPlanItem>, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
            }

            if (item.SupplementName.Trim().Length > SupplementNameMaxLength
                || item.Dosage.Trim().Length > DosageMaxLength)
            {
                return Result<List<NormalizedPlanItem>, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
            }

            if (item.DaysOfWeekMask is < 1 or > 127)
            {
                return Result<List<NormalizedPlanItem>, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
            }

            if (!TimeOnly.TryParse(item.TimeOfDay, out var parsedTime))
            {
                return Result<List<NormalizedPlanItem>, AppError>.Failure(new InvalidSupplementationError(Messages.FieldRequired));
            }

            normalizedItems.Add(new NormalizedPlanItem
            {
                SupplementName = item.SupplementName.Trim(),
                Dosage = item.Dosage.Trim(),
                TimeOfDay = parsedTime.ToTimeSpan(),
                DaysOfWeekMask = item.DaysOfWeekMask,
                Order = item.Order
            });
        }

        var sortedItems = normalizedItems
            .OrderBy(x => x.Order)
            .ThenBy(x => x.TimeOfDay)
            .ThenBy(x => x.SupplementName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<List<NormalizedPlanItem>, AppError>.Success(sortedItems);
    }

    private static bool IsScheduledOnDate(DaysOfWeekSet daysOfWeekMask, DateOnly date)
    {
        var normalizedDay = ((int)date.DayOfWeek + 6) % 7;
        var bit = 1 << normalizedDay;
        return ((int)daysOfWeekMask & bit) != 0;
    }

    private static string FormatTime(TimeSpan value)
    {
        return TimeOnly.FromTimeSpan(value).ToString("HH:mm");
    }

    private static SupplementPlanResult MapPlan(SupplementPlan plan)
    {
        return new SupplementPlanResult
        {
            Id = plan.Id,
            TrainerId = plan.TrainerId,
            TraineeId = plan.TraineeId,
            Name = plan.Name,
            Notes = plan.Notes,
            IsActive = plan.IsActive,
            CreatedAt = plan.CreatedAt,
            Items = plan.Items
                .OrderBy(x => x.Order)
                .ThenBy(x => x.TimeOfDay)
                .ThenBy(x => x.CreatedAt)
                .Select(item => new SupplementPlanItemResult
                {
                    Id = item.Id,
                    SupplementName = item.SupplementName,
                    Dosage = item.Dosage,
                    TimeOfDay = FormatTime(item.TimeOfDay),
                    DaysOfWeekMask = (int)item.DaysOfWeekMask,
                    Order = item.Order
                })
                .ToList()
        };
    }

    private sealed class NormalizedPlanItem
    {
        public string SupplementName { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public TimeSpan TimeOfDay { get; set; }
        public int DaysOfWeekMask { get; set; }
        public int Order { get; set; }
    }
}
