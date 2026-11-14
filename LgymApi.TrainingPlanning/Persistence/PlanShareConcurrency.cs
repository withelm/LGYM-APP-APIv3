using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.TrainingPlanning.Persistence;

internal static class PlanShareConcurrency
{
    public static bool IsConflict(Exception exception, Id<PlanReference> planId)
        => exception is DbUpdateConcurrencyException { Entries.Count: 1 } conflict
            && conflict.Entries[0].Entity is Plan plan
            && plan.Id == planId.Rebind<Plan>();
}
