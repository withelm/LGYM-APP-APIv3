using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using LgymApi.Payments.Providers.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class PaymentsRealFlowIntegrationTests : IntegrationTestBase
{
    protected override CustomWebApplicationFactory CreateFactory() =>
        CustomWebApplicationFactory.CreatePaymentsRealFlowFactory();

    [Test]
    public async Task CheckoutAndVerifiedWebhook_ReplayKeepsOnePaidCheckoutAndOneReceipt()
    {
        // Given
        var account = await SeedUserAsync("payments-real-flow", "payments-real-flow@example.com");
        Factory.Services.GetRequiredService<StripePaymentOptions>().Enabled.Should().BeTrue();
        SetAuthorizationHeader(account.Id);
        SetIdempotencyKey("payments-real-flow-checkout");

        // When
        using var checkoutResponse = await Client.PostAsJsonAsync("/api/payments/checkout", new { });

        // Then
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var checkoutDocument = JsonDocument.Parse(await checkoutResponse.Content.ReadAsStringAsync());
        var checkoutId = checkoutDocument.RootElement.GetProperty("checkoutId").GetString();
        checkoutId.Should().NotBeNullOrWhiteSpace();
        checkoutDocument.RootElement.GetProperty("checkoutUrl").GetString().Should().Be("https://checkout.test.local/scripted-session");

        var payload = Encoding.UTF8.GetBytes("scripted-payment-event");
        using var webhookRequest = CreateWebhookRequest(payload);
        using var webhookResponse = await Client.SendAsync(webhookRequest);
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var replayRequest = CreateWebhookRequest(payload);
        using var replayResponse = await Client.SendAsync(replayRequest);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedCheckout = await database.PaymentCheckouts.SingleAsync(item => item.Id.ToString() == checkoutId);
        var receipt = await database.PaymentEventReceipts.SingleAsync(item => item.ProviderEventId == "evt_scripted_payment");

        persistedCheckout.Status.Should().Be(PaymentCheckoutStatus.Paid);
        receipt.Status.Should().Be(PaymentEventReceiptStatus.Processed);
        receipt.PaymentCheckoutId.Should().Be(persistedCheckout.Id);
    }

    private static HttpRequestMessage CreateWebhookRequest(byte[] payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe")
        {
            Content = new ByteArrayContent(payload)
        };
        request.Headers.Add("Stripe-Signature", "scripted-valid-signature");
        return request;
    }
}
