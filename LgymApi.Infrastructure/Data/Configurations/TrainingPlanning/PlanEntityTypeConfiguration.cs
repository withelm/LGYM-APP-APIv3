using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.TrainingPlanning;

internal sealed class PlanEntityTypeConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");

        builder.HasIndex(p => p.ShareCode)
            .IsUnique()
            .HasFilter(TrainingPlanningConfigurationFilters.ActiveShareCodeFilter);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Plans)
            .HasForeignKey(p => p.UserId);
    }
}
