using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class PaymentCheckout : EntityBase<PaymentCheckout>
{
    public Id<User> AccountId { get; set; }
    public string ClientIdempotencyKey { get; set; } = string.Empty;
    public string? ProviderSessionReference { get; set; }
    public PaymentCheckoutStatus Status { get; set; }
}
