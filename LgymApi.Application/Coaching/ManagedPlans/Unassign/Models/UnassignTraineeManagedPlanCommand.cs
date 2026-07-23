using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.ManagedPlans.Unassign;

public sealed record UnassignTraineeManagedPlanCommand(Id<UserEntity> TrainerId, Id<UserEntity> TraineeId);
