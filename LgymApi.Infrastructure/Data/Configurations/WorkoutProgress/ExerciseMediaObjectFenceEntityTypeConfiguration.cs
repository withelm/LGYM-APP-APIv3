using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.WorkoutProgress;

internal sealed class ExerciseMediaObjectFenceEntityTypeConfiguration : IEntityTypeConfiguration<ExerciseMediaObjectFence>
{
    public void Configure(EntityTypeBuilder<ExerciseMediaObjectFence> builder)
    {
        builder.ToTable("ExerciseMediaObjectFences");
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.Property(x => x.ObjectKey).HasMaxLength(256);
        builder.Property(x => x.PublicUrl).HasMaxLength(1024);
        builder.HasIndex(x => x.ObjectKey).IsUnique();
        builder.HasIndex(x => x.LastCleanupAt);
    }
}
