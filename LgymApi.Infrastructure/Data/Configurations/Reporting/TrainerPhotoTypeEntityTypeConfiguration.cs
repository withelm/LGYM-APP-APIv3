using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Reporting;

internal sealed class TrainerPhotoTypeEntityTypeConfiguration : IEntityTypeConfiguration<TrainerPhotoType>
{
    public void Configure(EntityTypeBuilder<TrainerPhotoType> builder)
    {
        builder.ToTable("TrainerPhotoTypes");
        builder.Property(e => e.SystemKey).HasMaxLength(32);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(e => e.NameMode).HasConversion<string>();
        builder.HasIndex(e => new { e.TrainerId, e.SystemKey }).IsUnique().HasFilter("\"SystemKey\" IS NOT NULL");
        builder.HasIndex(e => new { e.TrainerId, e.NormalizedName }).IsUnique().HasFilter(ReportingConfigurationFilters.ActiveRowsFilter);
        builder.HasOne(e => e.Trainer).WithMany().HasForeignKey(e => e.TrainerId).OnDelete(DeleteBehavior.Restrict);
    }
}
