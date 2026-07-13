using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Infrastructure.Data.Configurations.Platform;

internal sealed class ActionExecutionLogEntityTypeConfiguration : IEntityTypeConfiguration<ActionExecutionLog>
{
    public void Configure(EntityTypeBuilder<ActionExecutionLog> builder)
    {
        builder.ToTable("ActionExecutionLogs");

        var idProp = builder.Property(e => e.Id);
        idProp.HasConversion(typeof(TypedIdValueConverter<ActionExecutionLog>));
        var idMetadata = builder.Metadata.FindProperty("Id");
        if (idMetadata != null)
        {
            var idComparerType = typeof(IdValueComparer<>).MakeGenericType(typeof(ActionExecutionLog));
            var idComparer = Activator.CreateInstance(idComparerType);
            if (idComparer != null)
            {
                var setMethod = idMetadata.GetType()
                    .GetMethod("SetValueComparer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                setMethod?.Invoke(idMetadata, new[] { idComparer });
            }
        }

        builder.Property(e => e.ActionType).HasConversion<string>();
        builder.Property(e => e.HandlerTypeName);
        builder.Property(e => e.Status).HasConversion<string>();

        builder.HasIndex(e => new { e.CommandEnvelopeId, e.Status })
            .HasFilter(PlatformConfigurationFilters.ActiveRowsFilter);
        builder.HasIndex(e => new { e.CommandEnvelopeId, e.ActionType })
            .HasFilter(PlatformConfigurationFilters.ActiveRowsFilter);
        builder.HasIndex(e => e.CreatedAt)
            .HasFilter(PlatformConfigurationFilters.ActiveRowsFilter);
    }
}
