using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Platform;

internal sealed class UserTutorialProgressEntityTypeConfiguration : IEntityTypeConfiguration<UserTutorialProgress>
{
    public void Configure(EntityTypeBuilder<UserTutorialProgress> builder)
    {
        builder.ToTable("UserTutorialProgresses");

        builder.Property(utp => utp.TutorialType).HasConversion<string>();

        builder.HasOne(utp => utp.User)
            .WithMany()
            .HasForeignKey(utp => utp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(utp => new { utp.UserId, utp.TutorialType })
            .IsUnique()
            .HasFilter(PlatformConfigurationFilters.ActiveRowsFilter);
        builder.HasIndex(utp => new { utp.UserId, utp.IsCompleted })
            .HasFilter(PlatformConfigurationFilters.ActiveRowsFilter);
    }
}
