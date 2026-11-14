using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class PhotoDeletionWorkItemSeeder : IEntitySeeder
{
    public int Order => 90;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("photo deletion work items");
        Id<PhotoDeletionWorkItem>.TryParse("f0b7152d-b6f8-48d4-90e8-ece0f1f55101", out var id);
        Id<CorrelationScope>.TryParse("f0b7152d-b6f8-48d4-90e8-ece0f1f55102", out var generation);
        if (await context.PhotoDeletionWorkItems.AnyAsync(item => item.Id == id, cancellationToken))
        {
            SeedOperationConsole.Skip("photo deletion work items");
            return;
        }

        var completedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        context.PhotoDeletionWorkItems.Add(new PhotoDeletionWorkItem
        {
            Id = id,
            DeletionGeneration = generation,
            StorageKey = "photos/demo/completed-deletion.jpg",
            NotBeforeUtc = completedAt,
            CompletedAtUtc = completedAt,
            CreatedAt = completedAt,
            UpdatedAt = completedAt
        });
        SeedOperationConsole.Done("photo deletion work items");
    }
}
