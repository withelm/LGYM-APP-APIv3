using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LgymApi.Payments.Providers.Stripe;

internal sealed record StripePaymentOptions
{
    public bool Enabled { get; init; }
    public string PriceId { get; init; } = string.Empty;
    public string SuccessUrl { get; init; } = string.Empty;
    public string CancelUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string WebhookSigningSecret { get; init; } = string.Empty;
}

internal static class StripePaymentOptionsFactory
{
    internal static StripePaymentOptions Create(IConfiguration configuration)
    {
        var options = configuration.GetSection("Payments").Get<StripePaymentOptions>() ?? new StripePaymentOptions();
        return options with
        {
            PriceId = options.PriceId.Trim(),
            SuccessUrl = options.SuccessUrl.Trim(),
            CancelUrl = options.CancelUrl.Trim(),
            ApiKey = options.ApiKey.Trim(),
            WebhookSigningSecret = options.WebhookSigningSecret.Trim()
        };
    }

    internal static void Validate(StripePaymentOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        if (!options.Enabled)
        {
            return;
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("Payments Stripe test mode cannot run in Production.");
        }

        if (!options.ApiKey.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Payments:ApiKey must be a Stripe test-mode secret key when payments are enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.PriceId) ||
            !Uri.TryCreate(options.SuccessUrl, UriKind.Absolute, out _) ||
            !Uri.TryCreate(options.CancelUrl, UriKind.Absolute, out _) ||
            !options.WebhookSigningSecret.StartsWith("whsec_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Enabled Payments configuration requires a test price, absolute return URLs, and a webhook signing secret.");
        }
    }
}
