using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Payments.Persistence.Configurations;

internal sealed class PaymentEventReceiptEntityTypeConfiguration : IEntityTypeConfiguration<PaymentEventReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentEventReceipt> builder)
    {
        builder.ToTable("PaymentEventReceipts");
        builder.Property(receipt => receipt.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(receipt => receipt.Status).HasConversion<string>();
        builder.HasIndex(receipt => receipt.ProviderEventId).IsUnique();
        builder.HasOne<PaymentCheckout>().WithMany().HasForeignKey(receipt => receipt.PaymentCheckoutId).OnDelete(DeleteBehavior.Restrict);
    }
}
