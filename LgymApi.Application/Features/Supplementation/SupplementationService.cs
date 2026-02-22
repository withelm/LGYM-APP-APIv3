using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
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

    public async Task<List<SupplementPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        var plans = await _supplementationRepository.GetPlansByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        return plans.Select(MapPlan).ToList();
    }

    public async Task<SupplementPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        var normalizedItems = ValidateAndNormalizeItems(command);

        var plan = new SupplementPlan
        {
            Id = Guid.NewGuid(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Name = command.Name.Trim(),
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            IsActive = false,
            IsDeleted = false,
            Items = normalizedItems.Select(item => new SupplementPlanItem
            {
                Id = Guid.NewGuid(),
                SupplementName = item.SupplementName,
                Dosage = item.Dosage,
                TimeOfDay = item.TimeOfDay,
                DaysOfWeekMask = item.DaysOfWeekMask,
                Order = item.Order
            }).ToList()
        };

        await _supplementationRepository.AddPlanAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapPlan(plan);
    }

    public async Task<SupplementPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        var normalizedItems = ValidateAndNormalizeItems(command);
        var plan = await EnsureOwnedPlanAsync(currentTrainer, traineeId, planId, cancellationToken);
        var wasActive = plan.IsActive;

        plan.IsDeleted = true;
        plan.IsActive = false;

        var newPlan = new SupplementPlan
        {
            Id = Guid.NewGuid(),
            TrainerId = currentTrainer.Id,
            TraineeId = traineeId,
            Name = command.Name.Trim(),
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            IsActive = wasActive,
            IsDeleted = false,
            Items = normalizedItems.Select(item => new SupplementPlanItem
            {
                Id = Guid.NewGuid(),
                SupplementName = item.SupplementName,
                Dosage = item.Dosage,
                TimeOfDay = item.TimeOfDay,
                DaysOfWeekMask = item.DaysOfWeekMask,
                Order = item.Order
            }).ToList()
        };

        await _supplementationRepository.AddPlanAsync(newPlan, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapPlan(newPlan);
    }

    public async Task DeleteTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        var plan = await EnsureOwnedPlanAsync(currentTrainer, traineeId, planId, cancellationToken);
        plan.IsDeleted = true;
        plan.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        var plan = await EnsureOwnedPlanAsync(currentTrainer, traineeId, planId, cancellationToken);

        var existingPlans = await _supplementationRepository.GetPlansByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        foreach (var candidate in existingPlans.Where(x => x.IsActive && x.Id != plan.Id))
        {
            candidate.IsActive = false;
        }

        plan.IsActive = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);

        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(traineeId, cancellationToken);
        if (activePlan == null || activePlan.TrainerId != currentTrainer.Id)
        {
            return;
        }

        activePlan.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<SupplementScheduleEntryResult>> GetActiveScheduleForDateAsync(UserEntity currentTrainee, DateOnly date, CancellationToken cancellationToken = default)
    {
        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(currentTrainee.Id, cancellationToken);
        if (activePlan == null)
        {
            return [];
        }

        var logs = await _supplementationRepository.GetIntakeLogsForPlanAsync(currentTrainee.Id, activePlan.Id, date, date, cancellationToken);
        var logsByPlanItem = logs.ToDictionary(x => x.PlanItemId, x => x);

        return activePlan.Items
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
    }

    public async Task<SupplementScheduleEntryResult> CheckOffIntakeAsync(UserEntity currentTrainee, CheckOffSupplementIntakeCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PlanItemId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (command.IntakeDate == default)
        {
            throw AppException.BadRequest(Messages.DateRequired);
        }

        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(currentTrainee.Id, cancellationToken);
        if (activePlan == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var planItem = activePlan.Items.FirstOrDefault(item => item.Id == command.PlanItemId);
        if (planItem == null || !IsScheduledOnDate(planItem.DaysOfWeekMask, command.IntakeDate))
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var existing = await _supplementationRepository.FindIntakeLogAsync(currentTrainee.Id, command.PlanItemId, command.IntakeDate, cancellationToken);
        if (existing == null)
        {
            existing = new SupplementIntakeLog
            {
                Id = Guid.NewGuid(),
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

        return new SupplementScheduleEntryResult
        {
            PlanItemId = planItem.Id,
            SupplementName = planItem.SupplementName,
            Dosage = planItem.Dosage,
            TimeOfDay = FormatTime(planItem.TimeOfDay),
            IntakeDate = command.IntakeDate,
            Taken = true,
            TakenAt = existing.TakenAt
        };
    }

    public async Task<SupplementComplianceSummaryResult> GetComplianceSummaryAsync(UserEntity currentTrainer, Guid traineeId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (toDate < fromDate)
        {
            throw AppException.BadRequest(Messages.InvalidDateRange);
        }

        var inclusiveRangeDays = toDate.DayNumber - fromDate.DayNumber + 1;
        if (inclusiveRangeDays > MaxComplianceRangeDays)
        {
            throw AppException.BadRequest(Messages.DateRangeTooLarge);
        }

        var activePlan = await _supplementationRepository.GetActivePlanForTraineeAsync(traineeId, cancellationToken);
        if (activePlan == null || activePlan.TrainerId != currentTrainer.Id)
        {
            return new SupplementComplianceSummaryResult
            {
                TraineeId = traineeId,
                FromDate = fromDate,
                ToDate = toDate,
                PlannedDoses = 0,
                TakenDoses = 0,
                AdherenceRate = 0
            };
        }

        var plannedDoses = 0;
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            plannedDoses += activePlan.Items.Count(item => IsScheduledOnDate(item.DaysOfWeekMask, date));
        }

        var logs = await _supplementationRepository.GetIntakeLogsForPlanAsync(traineeId, activePlan.Id, fromDate, toDate, cancellationToken);
        var takenDoses = logs.Count;
        var adherenceRate = plannedDoses == 0 ? 0 : Math.Round((double)takenDoses / plannedDoses * 100, 2);

        return new SupplementComplianceSummaryResult
        {
            TraineeId = traineeId,
            FromDate = fromDate,
            ToDate = toDate,
            PlannedDoses = plannedDoses,
            TakenDoses = takenDoses,
            AdherenceRate = adherenceRate
        };
    }

    private async Task EnsureTrainerOwnsTraineeAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken)
    {
        var isTrainer = await _roleRepository.UserHasRoleAsync(currentTrainer.Id, AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            throw AppException.Forbidden(Messages.TrainerRoleRequired);
        }

        if (traineeId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(currentTrainer.Id, traineeId, cancellationToken);
        if (link == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }
    }

    private async Task<SupplementPlan> EnsureOwnedPlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, CancellationToken cancellationToken)
    {
        if (planId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var plan = await _supplementationRepository.FindPlanByIdAsync(planId, cancellationToken);
        if (plan == null
            || plan.TrainerId != currentTrainer.Id
            || plan.TraineeId != traineeId
            || plan.IsDeleted)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return plan;
    }

    private static List<NormalizedPlanItem> ValidateAndNormalizeItems(UpsertSupplementPlanCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name)
            || command.Name.Trim().Length > PlanNameMaxLength
            || command.Items.Count == 0)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        if (command.Notes?.Trim().Length > PlanNotesMaxLength)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var normalizedItems = new List<NormalizedPlanItem>(command.Items.Count);
        foreach (var item in command.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SupplementName)
                || string.IsNullOrWhiteSpace(item.Dosage)
                || string.IsNullOrWhiteSpace(item.TimeOfDay))
            {
                throw AppException.BadRequest(Messages.FieldRequired);
            }

            if (item.SupplementName.Trim().Length > SupplementNameMaxLength
                || item.Dosage.Trim().Length > DosageMaxLength)
            {
                throw AppException.BadRequest(Messages.FieldRequired);
            }

            if (item.DaysOfWeekMask is < 1 or > 127)
            {
                throw AppException.BadRequest(Messages.FieldRequired);
            }

            if (!TimeOnly.TryParse(item.TimeOfDay, out var parsedTime))
            {
                throw AppException.BadRequest(Messages.FieldRequired);
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

        return normalizedItems
            .OrderBy(x => x.Order)
            .ThenBy(x => x.TimeOfDay)
            .ThenBy(x => x.SupplementName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsScheduledOnDate(int daysOfWeekMask, DateOnly date)
    {
        var normalizedDay = ((int)date.DayOfWeek + 6) % 7;
        var bit = 1 << normalizedDay;
        return (daysOfWeekMask & bit) != 0;
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
                    DaysOfWeekMask = item.DaysOfWeekMask,
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
