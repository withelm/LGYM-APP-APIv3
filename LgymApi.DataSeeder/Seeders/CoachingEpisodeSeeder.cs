using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class CoachingEpisodeSeeder : IEntitySeeder
{
    public int Order => 65;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("coaching episodes");
        if (seedContext.DemoUsers.Count < 2)
        {
            SeedOperationConsole.Skip("coaching episodes");
            return;
        }

        var trainerId = seedContext.DemoUsers[0].Id;
        var traineeId = seedContext.DemoUsers[1].Id;
        var existing = await context.CoachingEpisodes.AsNoTracking().FirstOrDefaultAsync(
            episode => episode.TrainerId == trainerId
                && episode.TraineeId == traineeId
                && episode.EndedAt == null,
            cancellationToken);
        if (existing is not null)
        {
            seedContext.CoachingEpisodes.Add(existing);
            SeedOperationConsole.Skip("coaching episodes");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            StartedAt = now,
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation,
            AuthorizationRevision = 0
        };
        await context.CoachingEpisodes.AddAsync(episode, cancellationToken);
        seedContext.CoachingEpisodes.Add(episode);
        SeedOperationConsole.Done("coaching episodes");
    }
}
