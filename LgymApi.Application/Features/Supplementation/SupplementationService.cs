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

public sealed partial class SupplementationService : ISupplementationService
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
