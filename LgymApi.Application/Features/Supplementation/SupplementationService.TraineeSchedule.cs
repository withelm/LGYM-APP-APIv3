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
}
