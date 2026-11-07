using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Coaching;

internal sealed class CoachingEpisodeEntityTypeConfiguration : IEntityTypeConfiguration<CoachingEpisode>
{
    public void Configure(EntityTypeBuilder<CoachingEpisode> builder)
    {
        builder.ToTable("CoachingEpisodes");

        builder.Property(episode => episode.AuthorizationRevision).IsConcurrencyToken();
        builder.Property(episode => episode.StartedAtProvenance).HasConversion<string>();
        builder.HasIndex(episode => new { episode.TrainerId, episode.TraineeId });

        builder.HasOne(episode => episode.Trainer)
            .WithMany()
            .HasForeignKey(episode => episode.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(episode => episode.Trainee)
            .WithMany()
            .HasForeignKey(episode => episode.TraineeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
