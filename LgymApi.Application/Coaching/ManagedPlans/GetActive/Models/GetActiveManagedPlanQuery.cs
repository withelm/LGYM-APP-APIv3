using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.ManagedPlans.GetActive;

public sealed record GetActiveManagedPlanQuery(Id<UserEntity> TraineeId);
