using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public sealed record ManagedPlanReadModel(
    Id<PlanEntity> Id,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt);
