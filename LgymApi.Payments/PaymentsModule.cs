using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using LgymApi.Payments.Persistence;
using LgymApi.Payments.Providers.Stripe;
using LgymApi.Payments.Contracts;

namespace LgymApi.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.AddScoped<IPaymentCheckoutRepository, PaymentCheckoutRepository>();
        services.AddScoped<IPaymentEventReceiptRepository, PaymentEventReceiptRepository>();
        return services;
    }

    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = StripePaymentOptionsFactory.Create(configuration);
        StripePaymentOptionsFactory.Validate(options, environment);

        services.AddPaymentsModule();
        services.AddSingleton(options);
        services.AddScoped<IStripeCheckoutGateway, StripeCheckoutGateway>();
        services.AddScoped<IStripeWebhookVerifier, StripeWebhookVerifier>();
        services.AddScoped<StripePaymentsService>();
        services.AddScoped<IPaymentsApi, PaymentsApi>();
        return services;
    }
}
