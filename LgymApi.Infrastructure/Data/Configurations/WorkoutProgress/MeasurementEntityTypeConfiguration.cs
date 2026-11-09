using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.WorkoutProgress;

internal sealed class MeasurementEntityTypeConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.ToTable("Measurements");

        builder.Property(e => e.BodyPart).HasConversion<string>();
        builder.Property(e => e.SourceReportSubmissionId).HasColumnType("uuid");
        builder.Property(e => e.SourceReportRequestId).HasColumnType("uuid");
        builder.Property(e => e.SourceCoachingEpisodeId).HasColumnType("uuid");
        builder.Property(e => e.SourceReportAcceptedAt).HasColumnType("timestamp with time zone");
        builder.Property(e => e.RecordedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(e => new { e.UserId, e.SourceCoachingEpisodeId, e.SourceReportAcceptedAt });
    }
}
