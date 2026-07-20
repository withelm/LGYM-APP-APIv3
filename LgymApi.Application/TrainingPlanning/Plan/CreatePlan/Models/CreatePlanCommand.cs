using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.CreatePlan;

public sealed record CreatePlanCommand(
    Id<UserEntity> CurrentUserId,
    Id<UserEntity> RouteUserId,
    string Name);
