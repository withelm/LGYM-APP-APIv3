using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Identity.Persistence.Configurations;

internal sealed class TrainerApplicationAttemptEntityTypeConfiguration : IEntityTypeConfiguration<TrainerApplicationAttempt>
{
    public void Configure(EntityTypeBuilder<TrainerApplicationAttempt> builder)
    {
        builder.ToTable("TrainerApplications");
        builder.Property(application => application.Status).HasConversion<string>().IsConcurrencyToken();
        builder.HasIndex(application => new { application.ApplicantId, application.Attempt }).IsUnique();
        builder.HasIndex(application => new { application.Status, application.CreatedAt });
        builder.HasIndex(application => application.ApplicantId)
            .IsUnique()
            .HasFilter(""" "Status" = 'PendingApproval' AND "IsDeleted" = FALSE """);
        builder.HasOne(application => application.Applicant).WithMany(user => user.TrainerApplications)
            .HasForeignKey(application => application.ApplicantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(application => application.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
