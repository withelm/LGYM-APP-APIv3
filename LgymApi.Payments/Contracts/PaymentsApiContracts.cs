using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Payments.Contracts;

public interface IPaymentsApi
{
    Task<PaymentCheckoutStartResult> CreateCheckoutAsync(PaymentAccountReference account, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<PaymentCheckoutStatusResult?> GetCheckoutStatusAsync(PaymentAccountReference account, Id<PaymentCheckout> checkoutId, CancellationToken cancellationToken = default);
    Task<PaymentWebhookResult> HandleWebhookAsync(ReadOnlyMemory<byte> rawBody, string signature, CancellationToken cancellationToken = default);
}

public sealed record PaymentCheckoutStartResult(Id<PaymentCheckout> CheckoutId, string CheckoutUrl);
public sealed record PaymentCheckoutStatusResult(Id<PaymentCheckout> CheckoutId, PaymentCheckoutStatus Status);
public enum PaymentWebhookResult { Processed, Ignored, BadRequest, PayloadTooLarge }
