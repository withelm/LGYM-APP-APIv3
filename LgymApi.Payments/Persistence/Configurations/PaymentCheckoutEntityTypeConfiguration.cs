using LgymApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LgymApi.Payments.Persistence.Configurations;

internal sealed class PaymentCheckoutEntityTypeConfiguration : IEntityTypeConfiguration<PaymentCheckout>
{
    public void Configure(EntityTypeBuilder<PaymentCheckout> builder)
    {
        builder.ToTable("PaymentCheckouts");
        builder.Property(checkout => checkout.ClientIdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(checkout => checkout.ProviderSessionReference).HasMaxLength(200);
        builder.Property(checkout => checkout.Status).HasConversion<string>();
        builder.HasIndex(checkout => checkout.ProviderSessionReference).IsUnique();
        builder.HasIndex(checkout => new { checkout.AccountId, checkout.ClientIdempotencyKey }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(checkout => checkout.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
