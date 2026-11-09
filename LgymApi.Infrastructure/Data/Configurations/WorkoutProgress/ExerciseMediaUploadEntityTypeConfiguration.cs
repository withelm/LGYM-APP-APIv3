using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.WorkoutProgress;

internal sealed class ExerciseMediaUploadEntityTypeConfiguration : IEntityTypeConfiguration<ExerciseMediaUpload>
{
    public void Configure(EntityTypeBuilder<ExerciseMediaUpload> builder)
    {
        builder.ToTable("ExerciseMediaUploads");
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.Property(x => x.OperationId).HasConversion<NullableTypedIdValueConverter<ExerciseMediaOperationReference>>();
        builder.HasIndex(x => new { x.ActorId, x.OperationId }).IsUnique();
        builder.HasIndex(x => new { x.ObjectKey, x.ExpiresAt });
        builder.Property(x => x.ObjectKey).HasMaxLength(256);
        builder.Property(x => x.Status).HasMaxLength(16);
        builder.Property(x => x.ContentType).HasMaxLength(32);
        builder.Property(x => x.SourceSha256).HasMaxLength(64);
        builder.Property(x => x.PublishedSha256).HasMaxLength(64);
        builder.Property(x => x.OperationHash).HasMaxLength(64);
        builder.Property(x => x.Extension).HasMaxLength(8);
    }
}
