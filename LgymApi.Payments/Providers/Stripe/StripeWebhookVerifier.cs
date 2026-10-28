using System.Text;
using Stripe;
using Stripe.Checkout;

namespace LgymApi.Payments.Providers.Stripe;

internal sealed class StripeWebhookVerifier(StripePaymentOptions options) : IStripeWebhookVerifier
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public bool TryVerify(ReadOnlyMemory<byte> rawBody, string signature, out StripeWebhookEvent stripeEvent)
    {
        stripeEvent = default!;
        try
        {
            var verifiedEvent = EventUtility.ConstructEvent(
                StrictUtf8.GetString(rawBody.Span),
                signature,
                options.WebhookSigningSecret,
                StripeWebhookLimits.SignatureToleranceSeconds);
            if (verifiedEvent.Data.Object is not Session session ||
                string.IsNullOrWhiteSpace(verifiedEvent.Id) ||
                string.IsNullOrWhiteSpace(session.Id))
            {
                return false;
            }

            stripeEvent = new StripeWebhookEvent(verifiedEvent.Id, verifiedEvent.Type, verifiedEvent.Livemode, session.Id);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
