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
        builder.HasIndex(receipt => receipt.ReportSubmissionId).IsUnique();
    }
}
