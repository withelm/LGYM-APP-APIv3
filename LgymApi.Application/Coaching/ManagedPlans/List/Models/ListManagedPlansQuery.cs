using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.ManagedPlans.List;

public sealed record ListManagedPlansQuery(Id<UserEntity> TrainerId, Id<UserEntity> TraineeId);
