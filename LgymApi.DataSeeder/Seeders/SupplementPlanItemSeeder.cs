using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class SupplementPlanItemSeeder : IEntitySeeder
{
    public int Order => 91;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("supplement plan items");
        if (seedContext.SupplementPlanItems.Count > 0)
        {
            SeedOperationConsole.Skip("supplement plan items");
            return;
        }

        var plans = seedContext.SupplementPlans;
        if (plans.Count == 0)
        {
            SeedOperationConsole.Skip("supplement plan items");
            return;
        }

        var existing = await context.SupplementPlanItems
            .AsNoTracking()
            .Select(item => new { item.PlanId, item.Order, item.TimeOfDay })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<(Guid PlanId, int Order, TimeSpan TimeOfDay)>(
            existing.Select(entry => (entry.PlanId, entry.Order, entry.TimeOfDay)));

        var addedAny = false;
        foreach (var plan in plans)
        {
            var items = new List<SupplementPlanItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PlanId = plan.Id,
                    SupplementName = "Whey protein",
                    Dosage = "30g",
                    DaysOfWeekMask = 127,
                    TimeOfDay = new TimeSpan(7, 30, 0),
                    Order = 1
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    PlanId = plan.Id,
                    SupplementName = "Creatine",
                    Dosage = "5g",
                    DaysOfWeekMask = 127,
                    TimeOfDay = new TimeSpan(18, 0, 0),
                    Order = 2
                }
            };

            foreach (var item in items)
            {
                if (!existingSet.Add((item.PlanId, item.Order, item.TimeOfDay)))
                {
                    continue;
                }

                await context.SupplementPlanItems.AddAsync(item, cancellationToken);
                seedContext.SupplementPlanItems.Add(item);
                addedAny = true;
            }
        }

        if (!addedAny)
        {
            SeedOperationConsole.Skip("supplement plan items");
            return;
        }

        SeedOperationConsole.Done("supplement plan items");
    }
}
