using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Progress.TrainingByDate;

public sealed record GetTrainingByDateQuery(
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    DateTime CreatedAt);
