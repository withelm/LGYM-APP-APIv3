using LgymApi.Application.Mapping.Core;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.TrainingPlanning.PlanDay.Persistence;
using LgymApi.Domain.ValueObjects;
using LgymApi.TrainingPlanning.Contracts;

namespace LgymApi.Application.TrainingPlanning.PlanDay;

internal sealed class PlanDayReferenceReadService(
    IPlanDayPersistence persistence,
    IMapper mapper) : IPlanDayReferenceReadService
{
    public async Task<PlanDayReferenceReadModel> GetByIdAsync(
        Id<PlanDayReference> planDayId,
        CancellationToken cancellationToken = default)
        => (await GetByIdsAsync([planDayId], cancellationToken))[0];

    public async Task<IReadOnlyList<PlanDayReferenceReadModel>> GetByIdsAsync(
        IReadOnlyList<Id<PlanDayReference>> planDayIds,
        CancellationToken cancellationToken = default)
    {
        var planDays = await persistence.GetPlanDaysByIdsAsync(planDayIds, cancellationToken);
        var plans = new Dictionary<Id<PlanReference>, PlanDayPlanPersistenceModel?>();
        foreach (var planId in planDays.Select(day => day.PlanId).Distinct())
        {
            plans[planId] = await persistence.FindPlanAsync(planId, cancellationToken);
        }

        var planDaysById = mapper
            .MapList<PlanDayReferenceSource, PlanDayReferenceReadModel>(
                planDays.Select(day => new PlanDayReferenceSource(day, plans[day.PlanId])))
            .ToDictionary(planDay => planDay.PlanDayId);

        return planDayIds
            .Select(planDayId => planDaysById.GetValueOrDefault(
                planDayId,
                new PlanDayReferenceReadModel(planDayId, Id<PlanReference>.Empty, string.Empty, false, false)))
            .ToList();
    }
}
