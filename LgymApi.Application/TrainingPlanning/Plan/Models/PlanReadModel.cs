using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.Models;

public sealed record PlanReadModel(
    Id<PlanEntity> Id,
    Id<UserEntity> UserId,
    string Name,
    bool IsActive,
    string? ShareCode);
