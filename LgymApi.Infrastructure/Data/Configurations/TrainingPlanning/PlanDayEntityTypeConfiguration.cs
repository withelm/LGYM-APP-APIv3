using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.TrainingPlanning;

internal sealed class PlanDayEntityTypeConfiguration : IEntityTypeConfiguration<PlanDay>
{
    public void Configure(EntityTypeBuilder<PlanDay> builder)
    {
        builder.ToTable("PlanDays");

        builder.HasOne(p => p.Plan)
            .WithMany(p => p.PlanDays)
            .HasForeignKey(p => p.PlanId);
    }
}
