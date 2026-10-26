using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Application.WorkoutProgress.TrainingExecution;

namespace LgymApi.Application.Coaching.Progress.TrainingByDate;

public sealed record GetTrainingByDateQuery(
    Id<AccountReference> TrainerId,
    Id<AccountReference> TraineeId,
    UtcTimeRange DateRange,
    IReadOnlyList<string> Cultures);
