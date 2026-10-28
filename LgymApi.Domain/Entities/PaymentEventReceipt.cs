using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class PaymentEventReceipt : EntityBase<PaymentEventReceipt>
{
    public Id<PaymentCheckout> PaymentCheckoutId { get; set; }
    public string ProviderEventId { get; set; } = string.Empty;
    public PaymentEventReceiptStatus Status { get; set; }
}
