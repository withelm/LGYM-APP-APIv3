using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.WorkoutProgress;

internal sealed class ExerciseEntityTypeConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");
        builder.Property(e => e.Image).IsConcurrencyToken();
        builder.Property(e => e.IsDeleted).IsConcurrencyToken();
        builder.Property(e => e.UserId).IsConcurrencyToken();

        builder.Property(e => e.BodyPart).HasConversion<string>();
        builder.Property(e => e.EloFormula)
            .HasConversion<string>()
            .HasDefaultValue(ExerciseEloFormula.Standard);
    }
}
