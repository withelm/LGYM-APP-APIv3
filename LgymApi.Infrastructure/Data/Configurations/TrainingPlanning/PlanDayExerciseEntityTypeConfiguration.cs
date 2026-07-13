using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.TrainingPlanning;

internal sealed class PlanDayExerciseEntityTypeConfiguration : IEntityTypeConfiguration<PlanDayExercise>
{
    public void Configure(EntityTypeBuilder<PlanDayExercise> builder)
    {
        builder.ToTable("PlanDayExercises");

        builder.HasOne(e => e.PlanDay)
            .WithMany(p => p.Exercises)
            .HasForeignKey(e => e.PlanDayId);

        builder.HasOne(e => e.Exercise)
            .WithMany()
            .HasForeignKey(e => e.ExerciseId);
    }
}
