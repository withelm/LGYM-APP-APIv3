using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Reporting;

internal sealed class PhotoDeletionWorkItemEntityTypeConfiguration : IEntityTypeConfiguration<PhotoDeletionWorkItem>
{
    public void Configure(EntityTypeBuilder<PhotoDeletionWorkItem> builder)
    {
        builder.ToTable("PhotoDeletionWorkItems");
        builder.Property(item => item.StorageKey).HasMaxLength(1024);
        builder.HasIndex(item => new { item.DeletionGeneration, item.StorageKey, item.IsPrefix }).IsUnique();
        builder.HasIndex(item => new { item.CompletedAtUtc, item.NotBeforeUtc });
        builder.Property(item => item.ContinuationToken).IsConcurrencyToken();
    }
}
