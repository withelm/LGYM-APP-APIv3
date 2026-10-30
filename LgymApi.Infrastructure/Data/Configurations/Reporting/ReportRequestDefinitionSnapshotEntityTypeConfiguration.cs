using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Reporting;

internal sealed class ReportRequestDefinitionSnapshotEntityTypeConfiguration : IEntityTypeConfiguration<ReportRequestDefinitionSnapshot>
{
    public void Configure(EntityTypeBuilder<ReportRequestDefinitionSnapshot> builder)
    {
        builder.ToTable("ReportRequestDefinitionSnapshots");
        builder.Property(e => e.DefinitionJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.Provenance).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.ReportRequestId).IsUnique();
        builder.HasOne(e => e.ReportRequest).WithOne(e => e.DefinitionSnapshot)
            .HasForeignKey<ReportRequestDefinitionSnapshot>(e => e.ReportRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
