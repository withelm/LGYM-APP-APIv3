using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.GetPlansList;

public sealed record GetPlansListQuery(Id<UserEntity> CurrentUserId, Id<UserEntity> RouteUserId);
