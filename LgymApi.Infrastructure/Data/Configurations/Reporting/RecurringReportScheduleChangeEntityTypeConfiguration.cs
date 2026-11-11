using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Reporting;

internal sealed class RecurringReportScheduleChangeEntityTypeConfiguration : IEntityTypeConfiguration<RecurringReportScheduleChange>
{
    public void Configure(EntityTypeBuilder<RecurringReportScheduleChange> builder)
    {
        builder.ToTable("RecurringReportScheduleChanges");

        builder.Property(change => change.Kind).HasConversion<string>();
        builder.Property(change => change.Status).HasConversion<string>();
        builder.Property(change => change.Reason).HasMaxLength(1000);
        builder.HasIndex(change => new { change.RecurringReportAssignmentId, change.Status });
        builder.HasIndex(
                change => new { change.RecurringReportAssignmentId, change.Kind, change.Status },
                "UX_RecurringReportScheduleChanges_PendingProposal")
            .IsUnique()
            .HasFilter("\"Kind\" = 'TraineeProposal' AND \"Status\" = 'Pending'");
        builder.HasIndex(
                change => new { change.RecurringReportAssignmentId, change.Kind, change.Status },
                "UX_RecurringReportScheduleChanges_ActiveOverride")
            .IsUnique()
            .HasFilter("\"Kind\" = 'TrainerOverride' AND \"Status\" = 'Active'");

        builder.HasOne(change => change.RecurringReportAssignment)
            .WithMany(assignment => assignment.ScheduleChanges)
            .HasForeignKey(change => change.RecurringReportAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(change => change.RequestedByUser)
            .WithMany()
            .HasForeignKey(change => change.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(change => change.ResolvedByUser)
            .WithMany()
            .HasForeignKey(change => change.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(change => change.SourceProposal)
            .WithMany()
            .HasForeignKey(change => change.SourceProposalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
