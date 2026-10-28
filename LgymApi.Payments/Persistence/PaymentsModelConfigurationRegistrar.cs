using LgymApi.Payments.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Payments.Persistence;

internal static class PaymentsModelConfigurationRegistrar
{
    internal static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PaymentCheckoutEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentEventReceiptEntityTypeConfiguration());
    }
}
