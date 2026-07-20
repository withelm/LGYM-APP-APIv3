using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.CopyPlan;

public sealed record CopyPlanCommand(Id<UserEntity> CurrentUserId, string ShareCode);
