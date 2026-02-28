using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class OutboxDeliverySeeder : IEntitySeeder
{
    public int Order => 73;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("outbox deliveries");

        var existing = await context.OutboxDeliveries
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
        {
            SeedOperationConsole.Skip("outbox deliveries");
            return;
        }

        seedContext.OutboxDeliveries.AddRange(existing);
        SeedOperationConsole.Done("outbox deliveries");
    }
}
