using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.CheckIsUserHavePlan;

public sealed record CheckIsUserHavePlanQuery(Id<UserEntity> CurrentUserId, Id<UserEntity> RouteUserId);
