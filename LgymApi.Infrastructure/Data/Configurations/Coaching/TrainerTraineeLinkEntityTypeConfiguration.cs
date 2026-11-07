using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Coaching;

internal sealed class TrainerTraineeLinkEntityTypeConfiguration : IEntityTypeConfiguration<TrainerTraineeLink>
{
    public void Configure(EntityTypeBuilder<TrainerTraineeLink> builder)
    {
        builder.ToTable("TrainerTraineeLinks");

        builder.HasIndex(e => e.TraineeId)
            .IsUnique()
            .HasFilter(CoachingConfigurationFilters.ActiveRowsFilter);

        builder.HasIndex(e => new { e.TrainerId, e.TraineeId });
        builder.HasIndex(e => e.EpisodeId).IsUnique();

        builder.HasOne(e => e.Trainer)
            .WithMany()
            .HasForeignKey(e => e.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Trainee)
            .WithMany()
            .HasForeignKey(e => e.TraineeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Episode)
            .WithMany()
            .HasForeignKey(e => e.EpisodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
