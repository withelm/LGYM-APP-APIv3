using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class OutboxMessageSeeder : IEntitySeeder
{
    public int Order => 72;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("outbox messages");

        var existing = await context.OutboxMessages
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
        {
            SeedOperationConsole.Skip("outbox messages");
            return;
        }

        seedContext.OutboxMessages.AddRange(existing);
        SeedOperationConsole.Done("outbox messages");
    }
}
