using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Payments.Providers.Stripe;

internal static class StripeWebhookLimits
{
    internal const int MaximumBodyBytes = 256 * 1024;
    internal const long SignatureToleranceSeconds = 300;
}

internal interface IStripeCheckoutGateway
{
    Task<StripeCheckoutSession> CreateAsync(StripeCheckoutCreateRequest request, CancellationToken cancellationToken);
    Task<StripeCheckoutSession> RetrieveAsync(string providerSessionReference, CancellationToken cancellationToken);
}

internal interface IStripeWebhookVerifier
{
    bool TryVerify(ReadOnlyMemory<byte> rawBody, string signature, out StripeWebhookEvent stripeEvent);
}

internal sealed record StripeCheckoutCreateRequest(
    string PriceId,
    long Quantity,
    IReadOnlyList<string> PaymentMethodTypes,
    string SuccessUrl,
    string CancelUrl,
    string ClientReferenceId,
    IReadOnlyDictionary<string, string> Metadata,
    string IdempotencyKey);

internal sealed record StripeCheckoutSession(
    string ProviderSessionReference,
    string CheckoutUrl,
    bool LiveMode,
    string PaymentStatus,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? PriceId = null,
    long? Quantity = null);

internal sealed record StripeWebhookEvent(
    string ProviderEventId,
    string EventType,
    bool LiveMode,
    string ProviderSessionReference);

internal sealed record CreatedStripeCheckout(
    Id<PaymentCheckout> CheckoutId,
    string ProviderSessionReference,
    string CheckoutUrl);

internal enum StripeWebhookHandlingResult
{
    Processed,
    Ignored,
    BadRequest,
    PayloadTooLarge
}
