using Stripe;
using Stripe.Checkout;

namespace LgymApi.Payments.Providers.Stripe;

internal sealed class StripeCheckoutGateway : IStripeCheckoutGateway
{
    private readonly SessionService _sessions;
    private readonly SessionLineItemService _lineItems;

    public StripeCheckoutGateway(StripePaymentOptions options)
    {
        var client = new StripeClient(options.ApiKey);
        _sessions = new SessionService(client);
        _lineItems = new SessionLineItemService(client);
    }

    public async Task<StripeCheckoutSession> CreateAsync(StripeCheckoutCreateRequest request, CancellationToken cancellationToken)
    {
        var session = await _sessions.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                PaymentMethodTypes = request.PaymentMethodTypes.ToList(),
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                ClientReferenceId = request.ClientReferenceId,
                Metadata = request.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value),
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = request.PriceId,
                        Quantity = request.Quantity
                    }
                ]
            },
            new RequestOptions { IdempotencyKey = request.IdempotencyKey },
            cancellationToken);

        return ToSession(session, request.PriceId, request.Quantity);
    }

    public async Task<StripeCheckoutSession> RetrieveAsync(string providerSessionReference, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(providerSessionReference, cancellationToken: cancellationToken);
        var lineItems = await _lineItems.ListAsync(providerSessionReference, cancellationToken: cancellationToken);
        var lineItem = lineItems.Data.SingleOrDefault();
        return ToSession(session, lineItem?.Price?.Id, lineItem?.Quantity);
    }

    private static StripeCheckoutSession ToSession(Session session, string? priceId, long? quantity)
    {
        return new StripeCheckoutSession(
            session.Id,
            session.Url ?? string.Empty,
            session.Livemode,
            session.PaymentStatus ?? string.Empty,
            session.Metadata,
            priceId,
            quantity);
    }
}
