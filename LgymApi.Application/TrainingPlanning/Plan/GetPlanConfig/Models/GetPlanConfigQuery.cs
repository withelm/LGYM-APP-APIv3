using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.GetPlanConfig;

public sealed record GetPlanConfigQuery(Id<UserEntity> CurrentUserId, Id<UserEntity> RouteUserId);
