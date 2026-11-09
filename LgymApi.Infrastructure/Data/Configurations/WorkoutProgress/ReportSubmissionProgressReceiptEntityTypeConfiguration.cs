using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.WorkoutProgress;

internal sealed class ReportSubmissionProgressReceiptEntityTypeConfiguration
    : IEntityTypeConfiguration<ReportSubmissionProgressReceipt>
{
    public void Configure(EntityTypeBuilder<ReportSubmissionProgressReceipt> builder)
    {
        builder.ToTable("ReportSubmissionProgressReceipts");
        builder.Property(receipt => receipt.SourceReportRequestId).HasColumnType("uuid");
        builder.Property(receipt => receipt.SourceCoachingEpisodeId).HasColumnType("uuid");
        builder.Property(receipt => receipt.SourceReportAcceptedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(receipt => receipt.ReportSubmissionId).IsUnique();
        builder.HasIndex(receipt => receipt.SourceCoachingEpisodeId);
    }
}
