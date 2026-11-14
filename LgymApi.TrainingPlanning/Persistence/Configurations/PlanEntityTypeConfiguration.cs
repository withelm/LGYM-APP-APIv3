using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.TrainingPlanning.Persistence.Configurations;

internal sealed class PlanEntityTypeConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");

        builder.Property(plan => plan.Purpose).HasConversion<string>().HasDefaultValue(PlanPurpose.Unclassified).IsConcurrencyToken();

        builder.HasIndex(plan => plan.ShareCode)
            .IsUnique()
            .HasFilter(TrainingPlanningConfigurationFilters.ActiveShareCodeFilter);

        builder.HasOne(plan => plan.User)
            .WithMany(user => user.Plans)
            .HasForeignKey(plan => plan.UserId);
    }
}
