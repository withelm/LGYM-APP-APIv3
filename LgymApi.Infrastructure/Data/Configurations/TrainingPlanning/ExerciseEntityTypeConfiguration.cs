using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.TrainingPlanning;

internal sealed class ExerciseEntityTypeConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");

        builder.Property(e => e.BodyPart).HasConversion<string>();
        builder.Property(e => e.EloFormula)
            .HasConversion<string>()
            .HasDefaultValue(ExerciseEloFormula.Standard);
    }
}
