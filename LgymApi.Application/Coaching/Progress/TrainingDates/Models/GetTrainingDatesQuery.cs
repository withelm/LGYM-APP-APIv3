using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Progress.TrainingDates;

public sealed record GetTrainingDatesQuery(Id<UserEntity> TrainerId, Id<UserEntity> TraineeId);
