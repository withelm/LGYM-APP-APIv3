using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class EmailNotificationSubscriptionSeeder : IEntitySeeder
{
    public int Order => 71;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("email notification subscriptions");

        var existing = await context.EmailNotificationSubscriptions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
        {
            SeedOperationConsole.Skip("email notification subscriptions");
            return;
        }

        seedContext.EmailNotificationSubscriptions.AddRange(existing);
        SeedOperationConsole.Done("email notification subscriptions");
    }
}
