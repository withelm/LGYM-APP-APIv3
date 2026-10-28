using System.Security.Cryptography;
using System.Text;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Payments.Persistence;
using LgymApi.Payments.Workflows;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Payments.Providers.Stripe;

internal sealed class StripePaymentsService(
    StripePaymentOptions options,
    IStripeCheckoutGateway checkoutGateway,
    IStripeWebhookVerifier webhookVerifier,
    IPaymentCheckoutRepository checkoutRepository,
    IPaymentEventReceiptRepository receiptRepository,
    IPaymentsWorkflowUnitOfWork unitOfWork)
{
    private const string CheckoutMetadataKey = "lgym_payment_checkout_id";
    private const string CheckoutCompletedEvent = "checkout.session.completed";

    internal async Task<CreatedStripeCheckout> CreateCheckoutAsync(
        Id<User> accountId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var normalizedKey = NormalizeClientIdempotencyKey(idempotencyKey);
        var checkout = await checkoutRepository.FindByAccountAndClientIdempotencyKeyAsync(accountId, normalizedKey, cancellationToken);
        if (checkout is not null && !string.IsNullOrEmpty(checkout.ProviderSessionReference))
        {
            var existingSession = await checkoutGateway.RetrieveAsync(checkout.ProviderSessionReference, cancellationToken);
            if (existingSession.LiveMode ||
                !string.Equals(existingSession.ProviderSessionReference, checkout.ProviderSessionReference, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(existingSession.CheckoutUrl))
            {
                throw new InvalidOperationException("Stripe returned an invalid Checkout Session.");
            }

            return new CreatedStripeCheckout(checkout.Id, checkout.ProviderSessionReference, existingSession.CheckoutUrl);
        }

        if (checkout is null)
        {
            checkout = new PaymentCheckout
            {
                Id = Id<PaymentCheckout>.New(),
                AccountId = accountId,
                ClientIdempotencyKey = normalizedKey,
                Status = PaymentCheckoutStatus.Created
            };
            await checkoutRepository.AddAsync(checkout, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var checkoutId = checkout.Id;
        var session = await checkoutGateway.CreateAsync(
            new StripeCheckoutCreateRequest(
                options.PriceId,
                1,
                ["card"],
                options.SuccessUrl,
                options.CancelUrl,
                accountId.ToString(),
                new Dictionary<string, string> { [CheckoutMetadataKey] = checkoutId.ToString() },
                CreateIdempotencyKey(accountId, normalizedKey)),
            cancellationToken);

        if (session.LiveMode)
        {
            throw new InvalidOperationException("Stripe returned a live-mode session while Payments test mode is enabled.");
        }

        if (string.IsNullOrWhiteSpace(session.ProviderSessionReference) || string.IsNullOrWhiteSpace(session.CheckoutUrl))
        {
            throw new InvalidOperationException("Stripe returned an invalid Checkout Session.");
        }

        checkout.ProviderSessionReference = session.ProviderSessionReference;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedStripeCheckout(checkoutId, session.ProviderSessionReference, session.CheckoutUrl);
    }

    internal async Task<StripeWebhookHandlingResult> HandleWebhookAsync(
        ReadOnlyMemory<byte> rawBody,
        string signature,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        if (rawBody.Length > StripeWebhookLimits.MaximumBodyBytes)
        {
            return StripeWebhookHandlingResult.PayloadTooLarge;
        }

        if (string.IsNullOrWhiteSpace(signature) || !webhookVerifier.TryVerify(rawBody, signature, out var stripeEvent))
        {
            return StripeWebhookHandlingResult.BadRequest;
        }

        if (!string.Equals(stripeEvent.EventType, CheckoutCompletedEvent, StringComparison.Ordinal) || stripeEvent.LiveMode)
        {
            return StripeWebhookHandlingResult.Ignored;
        }

        if (await receiptRepository.FindByProviderEventIdAsync(stripeEvent.ProviderEventId, cancellationToken) is not null)
        {
            return StripeWebhookHandlingResult.Ignored;
        }

        var checkout = await checkoutRepository.FindForUpdateByProviderSessionReferenceAsync(
            stripeEvent.ProviderSessionReference,
            cancellationToken);
        if (checkout is null)
        {
            return StripeWebhookHandlingResult.Ignored;
        }

        var session = await checkoutGateway.RetrieveAsync(stripeEvent.ProviderSessionReference, cancellationToken);
        var sessionMatchesCheckout =
            !session.LiveMode &&
            string.Equals(session.ProviderSessionReference, checkout.ProviderSessionReference, StringComparison.Ordinal) &&
            string.Equals(session.PaymentStatus, "paid", StringComparison.Ordinal) &&
            string.Equals(session.PriceId, options.PriceId, StringComparison.Ordinal) &&
            session.Quantity == 1 &&
            session.Metadata is not null &&
            session.Metadata.TryGetValue(CheckoutMetadataKey, out var checkoutMetadataId) &&
            string.Equals(checkoutMetadataId, checkout.Id.ToString(), StringComparison.Ordinal);

        await receiptRepository.AddAsync(new PaymentEventReceipt
        {
            Id = Id<PaymentEventReceipt>.New(),
            PaymentCheckoutId = checkout.Id,
            ProviderEventId = stripeEvent.ProviderEventId,
            Status = sessionMatchesCheckout ? PaymentEventReceiptStatus.Processed : PaymentEventReceiptStatus.Rejected
        }, cancellationToken);

        if (sessionMatchesCheckout && checkout.Status == PaymentCheckoutStatus.Created)
        {
            checkout.Status = PaymentCheckoutStatus.Paid;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return sessionMatchesCheckout ? StripeWebhookHandlingResult.Processed : StripeWebhookHandlingResult.Ignored;
        }
        catch (DbUpdateException)
        {
            if (await receiptRepository.FindByProviderEventIdAsync(stripeEvent.ProviderEventId, cancellationToken) is not null)
            {
                return StripeWebhookHandlingResult.Ignored;
            }

            throw;
        }
    }

    internal string CreateIdempotencyKey(Id<User> accountId, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        }

        var normalizedKey = NormalizeClientIdempotencyKey(idempotencyKey);
        var input = $"{accountId}\n{normalizedKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"lgym-checkout-{Convert.ToHexStringLower(hash)}";
    }

    private static string NormalizeClientIdempotencyKey(string idempotencyKey) => idempotencyKey.Trim();

    private void EnsureEnabled()
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException("Payments is disabled.");
        }
    }
}
