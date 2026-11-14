using LgymApi.Domain.ValueObjects;
using LgymApi.TrainingPlanning.Contracts;
using LgymApi.Identity.Contracts;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.TrainingPlanning.Contracts.PlanDay;

public sealed record PlanDayReferenceReadModel(
    Id<PlanDayReference> PlanDayId,
    Id<PlanReference> PlanId,
    string Name,
    bool Exists,
    bool IsDeleted,
    Id<AccountReference> OwnerId = default,
    PlanPurpose Purpose = PlanPurpose.Unclassified);
