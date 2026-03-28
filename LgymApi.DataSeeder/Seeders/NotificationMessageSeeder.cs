using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class NotificationMessageSeeder : IEntitySeeder
{
    public int Order => 70;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("email notification logs");
        if (seedContext.NotificationMessages.Count > 0)
        {
            SeedOperationConsole.Skip("email notification logs");
            return;
        }

        var existing = await context.NotificationMessages
            .AsNoTracking()
            .Select(message => new { message.Channel, message.Type, message.CorrelationId, message.Recipient })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<(NotificationChannel Channel, EmailNotificationType Type, Id<CorrelationScope> CorrelationId, Email Recipient)>(
            existing.Select(entry => (entry.Channel, entry.Type, entry.CorrelationId, entry.Recipient)));

        var messages = new List<NotificationMessage>
        {
            new()
            {
                Id = Id<NotificationMessage>.New(),
                Channel = NotificationChannel.Email,
                Type = EmailNotificationTypes.TrainerInvitation,
                CorrelationId = Id<CorrelationScope>.New(),
                Recipient = "trainee@lgym.app",
                PayloadJson = "{\"template\":\"trainer-invite\"}",
                Status = EmailNotificationStatus.Sent,
                Attempts = 1,
                LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-15)
            },
            new()
            {
                Id = Id<NotificationMessage>.New(),
                Channel = NotificationChannel.Email,
                Type = EmailNotificationTypes.Welcome,
                CorrelationId = Id<CorrelationScope>.New(),
                Recipient = "trainee@lgym.app",
                PayloadJson = "{\"template\":\"welcome\"}",
                Status = EmailNotificationStatus.Pending,
                Attempts = 0
            }
        };

        var addedAny = false;
        foreach (var message in messages)
        {
            if (!existingSet.Add((message.Channel, message.Type, message.CorrelationId, message.Recipient)))
            {
                continue;
            }

            await context.NotificationMessages.AddAsync(message, cancellationToken);
            seedContext.NotificationMessages.Add(message);
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
