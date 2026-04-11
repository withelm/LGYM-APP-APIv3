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
}
