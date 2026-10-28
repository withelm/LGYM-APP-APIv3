using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.IntegrationTests.Authorization;
using LgymApi.Resources;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class PaymentsApiTests : IntegrationTestBase
{
    [Test]
    [AuthorizationEvidence("POST", "/api/payments/checkout", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/payments/checkout", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/payments/checkout", "own", "no-client-subject")]
    public async Task CreateCheckout_UsesAuthenticatedAccountAndIdempotencyHeader()
    {
        var account = await SeedUserAsync("payments-owner", "payments-owner@example.com");
        SetAuthorizationHeader(account.Id);
        SetIdempotencyKey("payments-checkout-owner");

        using var response = await Client.PostAsJsonAsync("/api/payments/checkout", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("checkoutId").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("checkoutUrl").GetString().Should().Be("https://checkout.test.local/session");
        body.RootElement.TryGetProperty("price", out _).Should().BeFalse();
        body.RootElement.TryGetProperty("accountId", out _).Should().BeFalse();
    }

    [Test]
    public async Task CreateCheckout_RejectsAnonymousAndClientControlledInput()
    {
        using var anonymous = await Client.PostAsJsonAsync("/api/payments/checkout", new { });
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var account = await SeedUserAsync("payments-input", "payments-input@example.com");
        SetAuthorizationHeader(account.Id);
        SetIdempotencyKey("payments-checkout-invalid");
        using var invalid = await Client.PostAsJsonAsync("/api/payments/checkout", new { price = "price_client", quantity = 5, status = "Paid" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestCase("en")]
    [TestCase("pl")]
    public async Task CreateCheckout_RejectsClientControlledInputWithLocalizedValidationMessage(string culture)
    {
        var account = await SeedUserAsync($"payments-localized-{culture}", $"payments-localized-{culture}@example.com");
        SetAuthorizationHeader(account.Id);
        SetIdempotencyKey($"payments-checkout-localized-{culture}");
        var expectedMessage = GetMessageForCulture(culture, () => Messages.PaymentCheckoutClientFieldsNotAllowed);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/checkout")
        {
            Content = JsonContent.Create(new { price = "price_client" })
        };
        request.Headers.AcceptLanguage.ParseAdd(culture);

        using var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors").GetProperty("additionalProperties")[0].GetString().Should().Be(expectedMessage);
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/payments/checkouts/{checkoutId}", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/payments/checkouts/{checkoutId}", "own", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/payments/checkouts/{checkoutId}", "own", "foreign-object-denial-no-mutation")]
    public async Task GetCheckoutStatus_AllowsOnlyTheOwningAccount()
    {
        var owner = await SeedUserAsync("payments-status-owner", "payments-status-owner@example.com");
        var foreign = await SeedUserAsync("payments-status-foreign", "payments-status-foreign@example.com");
        var checkout = PaymentCheckoutTestStore.Create(owner.Id);
        SetAuthorizationHeader(owner.Id);

        using var own = await Client.GetAsync($"/api/payments/checkouts/{checkout.Id}");
        own.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthorizationHeader(foreign.Id);
        using var denied = await Client.GetAsync($"/api/payments/checkouts/{checkout.Id}");
        denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/payments/webhooks/stripe", "public", "anonymous-intended-behavior")]
    public async Task Webhook_RequiresValidSignatureAndIsReplaySafe()
    {
        var payload = Encoding.UTF8.GetBytes("valid-payment-event");
        using var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe")
        {
            Content = new ByteArrayContent(payload)
        };
        invalidRequest.Headers.Add("Stripe-Signature", "invalid");
        using var invalid = await Client.SendAsync(invalidRequest);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe")
        {
            Content = new ByteArrayContent(payload)
        };
        validRequest.Headers.Add("Stripe-Signature", "test-valid-signature");
        using var valid = await Client.SendAsync(validRequest);
        valid.StatusCode.Should().Be(HttpStatusCode.OK);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe")
        {
            Content = new ByteArrayContent(payload)
        };
        replayRequest.Headers.Add("Stripe-Signature", "test-valid-signature");
        using var replay = await Client.SendAsync(replayRequest);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        PaymentCheckoutTestStore.ProcessedWebhookCount.Should().Be(1);
    }

    [Test]
    public async Task Webhook_RejectsChunkedPayloadExceedingMaximumBodySize()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe")
        {
            Content = new ChunkedContent(new byte[(256 * 1024) + 1])
        };
        request.Headers.Add("Stripe-Signature", "test-valid-signature");

        using var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private static string GetMessageForCulture(string cultureName, Func<string> getMessage)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return getMessage();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private sealed class ChunkedContent(byte[] payload) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(payload).AsTask();
        }
    }
}
