using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.ManagedPlans.Create;

public sealed record CreateTraineeManagedPlanCommand(
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    string Name);
