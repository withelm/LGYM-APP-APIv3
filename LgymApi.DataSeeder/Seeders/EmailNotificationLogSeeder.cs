using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class EmailNotificationLogSeeder : IEntitySeeder
{
    public int Order => 70;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("email notification logs");
        if (seedContext.EmailNotificationLogs.Count > 0)
        {
            SeedOperationConsole.Skip("email notification logs");
            return;
        }

        var existing = await context.EmailNotificationLogs
            .AsNoTracking()
            .Select(log => new { log.Type, log.CorrelationId, log.RecipientEmail })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<(string Type, Guid CorrelationId, string RecipientEmail)>(
            existing.Select(entry => (entry.Type, entry.CorrelationId, entry.RecipientEmail)));

        var logs = new List<EmailNotificationLog>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Type = "TrainerInvitation",
                CorrelationId = Guid.NewGuid(),
                RecipientEmail = "trainee@lgym.app",
                PayloadJson = "{\"template\":\"trainer-invite\"}",
                Status = EmailNotificationStatus.Sent,
                Attempts = 1,
                LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-15)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Type = "ReportRequest",
                CorrelationId = Guid.NewGuid(),
                RecipientEmail = "trainee@lgym.app",
                PayloadJson = "{\"template\":\"report-request\"}",
                Status = EmailNotificationStatus.Pending,
                Attempts = 0
            }
        };

        var addedAny = false;
        foreach (var log in logs)
        {
            if (!existingSet.Add((log.Type, log.CorrelationId, log.RecipientEmail)))
            {
                continue;
            }

            await context.EmailNotificationLogs.AddAsync(log, cancellationToken);
            seedContext.EmailNotificationLogs.Add(log);
            addedAny = true;
        }

        if (!addedAny)
        {
            SeedOperationConsole.Skip("email notification logs");
            return;
        }

        SeedOperationConsole.Done("email notification logs");
    }
}
