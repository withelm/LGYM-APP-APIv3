using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Payments.Contracts;
using LgymApi.Payments.Persistence;
using LgymApi.Payments.Providers.Stripe;

namespace LgymApi.Payments;

internal sealed class PaymentsApi(StripePaymentsService stripePayments, IPaymentCheckoutRepository checkoutRepository) : IPaymentsApi
{
    public async Task<PaymentCheckoutStartResult> CreateCheckoutAsync(PaymentAccountReference account, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var accountId = Id<User>.TryParse(account.AccountId.ToString(), out var parsedAccountId) ? parsedAccountId : Id<User>.Empty;
        var checkout = await stripePayments.CreateCheckoutAsync(accountId, idempotencyKey, cancellationToken);
        return new PaymentCheckoutStartResult(checkout.CheckoutId, checkout.CheckoutUrl);
    }

    public async Task<PaymentCheckoutStatusResult?> GetCheckoutStatusAsync(PaymentAccountReference account, Id<PaymentCheckout> checkoutId, CancellationToken cancellationToken = default)
    {
        var accountId = Id<User>.TryParse(account.AccountId.ToString(), out var parsedAccountId) ? parsedAccountId : Id<User>.Empty;
        var checkout = await checkoutRepository.FindByIdAsync(checkoutId, cancellationToken);
        return checkout is null || checkout.AccountId != accountId
            ? null
            : new PaymentCheckoutStatusResult(checkout.Id, checkout.Status);
    }

    public async Task<PaymentWebhookResult> HandleWebhookAsync(ReadOnlyMemory<byte> rawBody, string signature, CancellationToken cancellationToken = default) =>
        await stripePayments.HandleWebhookAsync(rawBody, signature, cancellationToken) switch
        {
            StripeWebhookHandlingResult.Processed => PaymentWebhookResult.Processed,
            StripeWebhookHandlingResult.Ignored => PaymentWebhookResult.Ignored,
            StripeWebhookHandlingResult.BadRequest => PaymentWebhookResult.BadRequest,
            StripeWebhookHandlingResult.PayloadTooLarge => PaymentWebhookResult.PayloadTooLarge,
            _ => throw new InvalidOperationException("Unknown payment webhook result.")
        };
}
