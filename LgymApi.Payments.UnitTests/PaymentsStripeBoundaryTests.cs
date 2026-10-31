using FluentAssertions;
using System.Security.Cryptography;
using System.Text;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Payments.Persistence;
using LgymApi.Payments.Providers.Stripe;
using LgymApi.Payments.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace LgymApi.Payments.UnitTests.Stripe;

[TestFixture]
public sealed class PaymentsStripeBoundaryTests
{
    [Test]
    public void StripeOptions_WhenMissing_AreDisabledByDefault()
    {
        var options = StripePaymentOptionsFactory.Create(new ConfigurationBuilder().Build());

        options.Enabled.Should().BeFalse();
    }

    [Test]
    public void StripeOptions_WhenEnabledInProduction_RejectsStartup()
    {
        var options = CreateOptions();

        var action = () => StripePaymentOptionsFactory.Validate(options, new TestHostEnvironment { EnvironmentName = Environments.Production });

        action.Should().Throw<InvalidOperationException>().WithMessage("*Production*");
    }

    [TestCase("sk_live_secret")]
    [TestCase("pk_live_key")]
    public void StripeOptions_WhenEnabledWithLiveKey_RejectsStartup(string apiKey)
    {
        var options = CreateOptions() with { ApiKey = apiKey };

        var action = () => StripePaymentOptionsFactory.Validate(options, new TestHostEnvironment());

        action.Should().Throw<InvalidOperationException>().WithMessage("*test-mode*");
    }

    [Test]
    public async Task CreateCheckout_UsesConfiguredPaymentDetailsAndStableInternalMetadata()
    {
        var gateway = new FakeGateway { CreateResponse = new StripeCheckoutSession("cs_test_123", "https://checkout.stripe.test/session", false, "paid") };
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        var service = CreateService(gateway, checkoutRepository, receiptRepository);
        var accountId = Id<User>.New();

        var result = await service.CreateCheckoutAsync(accountId, "  client-key  ");

        result.ProviderSessionReference.Should().Be("cs_test_123");
        result.CheckoutUrl.Should().Be("https://checkout.stripe.test/session");
        gateway.CreateRequests.Should().ContainSingle();
        gateway.CreateRequests[0].PriceId.Should().Be("price_test_configured");
        gateway.CreateRequests[0].Quantity.Should().Be(1);
        gateway.CreateRequests[0].PaymentMethodTypes.Should().ContainSingle().Which.Should().Be("card");
        gateway.CreateRequests[0].SuccessUrl.Should().Be("https://app.example.test/payments/success");
        gateway.CreateRequests[0].CancelUrl.Should().Be("https://app.example.test/payments/cancel");
        gateway.CreateRequests[0].Metadata.Should().ContainKey("lgym_payment_checkout_id");
        gateway.CreateRequests[0].IdempotencyKey.Should().Be(service.CreateIdempotencyKey(accountId, "client-key"));
        await checkoutRepository.Received(1).AddAsync(
            Arg.Is<PaymentCheckout>(checkout =>
                checkout.ProviderSessionReference == "cs_test_123" &&
                checkout.AccountId == accountId &&
                checkout.Status == PaymentCheckoutStatus.Created),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateCheckout_WhenStripeReturnsLiveSession_RejectsAfterPersistingTheStableLocalIdentity()
    {
        var gateway = new FakeGateway { CreateResponse = new StripeCheckoutSession("cs_live_123", "https://checkout.stripe.test/session", true, "paid") };
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        var service = CreateService(gateway, checkoutRepository, Substitute.For<IPaymentEventReceiptRepository>());

        var action = () => service.CreateCheckoutAsync(Id<User>.New(), "key");

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*test mode*");
        await checkoutRepository.Received(1).AddAsync(
            Arg.Is<PaymentCheckout>(checkout => checkout.ProviderSessionReference == null && checkout.Status == PaymentCheckoutStatus.Created),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleWebhook_WhenBodyIsOversized_ReturnsPayloadTooLargeWithoutVerification()
    {
        var verifier = new FakeVerifier();
        var service = CreateService(new FakeGateway(), Substitute.For<IPaymentCheckoutRepository>(), Substitute.For<IPaymentEventReceiptRepository>(), verifier);

        var result = await service.HandleWebhookAsync(new byte[StripeWebhookLimits.MaximumBodyBytes + 1], "signature");

        result.Should().Be(StripeWebhookHandlingResult.PayloadTooLarge);
        verifier.VerificationAttempts.Should().Be(0);
    }

    [Test]
    public void StripeWebhookVerifier_RequiresTheExactRawBodyAndCurrentSignature()
    {
        const string payload = "{\"id\":\"evt_123\",\"object\":\"event\",\"api_version\":\"2025-03-31.basil\",\"created\":1743379200,\"data\":{\"object\":{\"id\":\"cs_test_123\",\"object\":\"checkout.session\",\"livemode\":false,\"payment_status\":\"paid\"}},\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null,\"idempotency_key\":null},\"type\":\"checkout.session.completed\"}";
        var verifier = new StripeWebhookVerifier(CreateOptions());
        var signature = CreateStripeSignature(payload, "whsec_configured", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        verifier.TryVerify(Encoding.UTF8.GetBytes(payload), signature, out var verified).Should().BeTrue();
        verified.ProviderEventId.Should().Be("evt_123");
        verifier.TryVerify(Encoding.UTF8.GetBytes(payload + " "), signature, out _).Should().BeFalse();
        verifier.TryVerify(Encoding.UTF8.GetBytes(payload), CreateStripeSignature(payload, "whsec_configured", DateTimeOffset.UtcNow.AddSeconds(-301).ToUnixTimeSeconds()), out _).Should().BeFalse();
    }

    [Test]
    public async Task HandleWebhook_WhenSignatureIsInvalid_ReturnsBadRequestWithoutMutation()
    {
        var verifier = new FakeVerifier();
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        var service = CreateService(new FakeGateway(), checkoutRepository, receiptRepository, verifier);

        var result = await service.HandleWebhookAsync("{}"u8.ToArray(), "invalid");

        result.Should().Be(StripeWebhookHandlingResult.BadRequest);
        await checkoutRepository.DidNotReceiveWithAnyArgs().FindForUpdateByProviderSessionReferenceAsync(default!, default);
        await receiptRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Test]
    public async Task HandleWebhook_WhenServerSessionIsPaidAndMatches_TransitionsCreatedCheckoutToPaid()
    {
        var accountId = Id<User>.New();
        var checkout = new PaymentCheckout
        {
            Id = Id<PaymentCheckout>.New(),
            AccountId = accountId,
            ProviderSessionReference = "cs_test_123",
            Status = PaymentCheckoutStatus.Created
        };
        var verifier = VerifiedEvent("evt_123", "cs_test_123");
        var gateway = new FakeGateway { RetrieveResponse = new StripeCheckoutSession(
            "cs_test_123",
            "https://checkout.stripe.test/session",
            false,
            "paid",
            new Dictionary<string, string> { ["lgym_payment_checkout_id"] = checkout.Id.ToString() },
            "price_test_configured",
            1) };
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        checkoutRepository.FindForUpdateByProviderSessionReferenceAsync("cs_test_123", Arg.Any<CancellationToken>()).Returns(checkout);
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        receiptRepository.FindByProviderEventIdAsync("evt_123", Arg.Any<CancellationToken>()).Returns((PaymentEventReceipt?)null);
        var service = CreateService(gateway, checkoutRepository, receiptRepository, verifier);

        var result = await service.HandleWebhookAsync("{}"u8.ToArray(), "signature");

        result.Should().Be(StripeWebhookHandlingResult.Processed);
        checkout.Status.Should().Be(PaymentCheckoutStatus.Paid);
        await receiptRepository.Received(1).AddAsync(
            Arg.Is<PaymentEventReceipt>(receipt => receipt.ProviderEventId == "evt_123" && receipt.Status == PaymentEventReceiptStatus.Processed),
            Arg.Any<CancellationToken>());
    }

    [TestCase("unpaid")]
    [TestCase("no_payment_required")]
    public async Task HandleWebhook_WhenServerSessionIsNotPaid_NeverTransitionsCheckout(string paymentStatus)
    {
        var checkout = new PaymentCheckout
        {
            Id = Id<PaymentCheckout>.New(),
            AccountId = Id<User>.New(),
            ProviderSessionReference = "cs_test_123",
            Status = PaymentCheckoutStatus.Created
        };
        var gateway = new FakeGateway { RetrieveResponse = new StripeCheckoutSession(
            "cs_test_123", "https://checkout.stripe.test/session", false, paymentStatus,
            new Dictionary<string, string> { ["lgym_payment_checkout_id"] = checkout.Id.ToString() }, "price_test_configured", 1) };
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        checkoutRepository.FindForUpdateByProviderSessionReferenceAsync("cs_test_123", Arg.Any<CancellationToken>()).Returns(checkout);
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        receiptRepository.FindByProviderEventIdAsync("evt_123", Arg.Any<CancellationToken>()).Returns((PaymentEventReceipt?)null);
        var service = CreateService(gateway, checkoutRepository, receiptRepository, VerifiedEvent("evt_123", "cs_test_123"));

        var result = await service.HandleWebhookAsync("{}"u8.ToArray(), "signature");

        result.Should().Be(StripeWebhookHandlingResult.Ignored);
        checkout.Status.Should().Be(PaymentCheckoutStatus.Created);
        await receiptRepository.Received(1).AddAsync(
            Arg.Is<PaymentEventReceipt>(receipt => receipt.Status == PaymentEventReceiptStatus.Rejected),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleWebhook_WhenEventWasAlreadyReceived_IsIdempotentWithoutProviderCall()
    {
        var verifier = VerifiedEvent("evt_123", "cs_test_123");
        var gateway = new FakeGateway();
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        receiptRepository.FindByProviderEventIdAsync("evt_123", Arg.Any<CancellationToken>()).Returns(new PaymentEventReceipt());
        var service = CreateService(gateway, Substitute.For<IPaymentCheckoutRepository>(), receiptRepository, verifier);

        var result = await service.HandleWebhookAsync("{}"u8.ToArray(), "signature");

        result.Should().Be(StripeWebhookHandlingResult.Ignored);
        gateway.RetrieveRequests.Should().BeEmpty();
    }

    [Test]
    public async Task HandleWebhook_WhenCheckoutIsUnknown_IsIgnoredWithoutProviderCallOrReceipt()
    {
        var gateway = new FakeGateway();
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        checkoutRepository.FindForUpdateByProviderSessionReferenceAsync("cs_unknown", Arg.Any<CancellationToken>()).Returns((PaymentCheckout?)null);
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        receiptRepository.FindByProviderEventIdAsync("evt_unknown", Arg.Any<CancellationToken>()).Returns((PaymentEventReceipt?)null);
        var service = CreateService(gateway, checkoutRepository, receiptRepository, VerifiedEvent("evt_unknown", "cs_unknown"));

        var result = await service.HandleWebhookAsync("{}"u8.ToArray(), "signature");

        result.Should().Be(StripeWebhookHandlingResult.Ignored);
        gateway.RetrieveRequests.Should().BeEmpty();
        await receiptRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Test]
    public async Task HandleWebhook_WhenVerifiedEventIsLiveMode_IsIgnoredBeforePersistence()
    {
        var verifier = new FakeVerifier { Event = new StripeWebhookEvent("evt_live", "checkout.session.completed", true, "cs_live") };
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        var service = CreateService(new FakeGateway(), checkoutRepository, receiptRepository, verifier);

        var result = await service.HandleWebhookAsync("{}"u8.ToArray(), "signature");

        result.Should().Be(StripeWebhookHandlingResult.Ignored);
        await checkoutRepository.DidNotReceiveWithAnyArgs().FindForUpdateByProviderSessionReferenceAsync(default!, default);
        await receiptRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    private static StripePaymentsService CreateService(
        IStripeCheckoutGateway gateway,
        IPaymentCheckoutRepository checkoutRepository,
        IPaymentEventReceiptRepository receiptRepository,
        IStripeWebhookVerifier? verifier = null)
    {
        var unitOfWork = Substitute.For<IPaymentsWorkflowUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return new StripePaymentsService(CreateOptions(), gateway, verifier ?? new FakeVerifier(), checkoutRepository, receiptRepository, unitOfWork);
    }

    private static IStripeWebhookVerifier VerifiedEvent(string eventId, string sessionReference)
    {
        return new FakeVerifier { Event = new StripeWebhookEvent(eventId, "checkout.session.completed", false, sessionReference) };
    }

    private static StripePaymentOptions CreateOptions() => new()
    {
        Enabled = true,
        PriceId = "price_test_configured",
        SuccessUrl = "https://app.example.test/payments/success",
        CancelUrl = "https://app.example.test/payments/cancel",
        ApiKey = "sk_test_configured",
        WebhookSigningSecret = "whsec_configured"
    };

    private static string CreateStripeSignature(string payload, string secret, long timestamp)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return $"t={timestamp},v1={signature}";
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = nameof(PaymentsStripeBoundaryTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = Substitute.For<Microsoft.Extensions.FileProviders.IFileProvider>();
    }

    private sealed class FakeGateway : IStripeCheckoutGateway
    {
        public StripeCheckoutSession? CreateResponse { get; init; }
        public StripeCheckoutSession? RetrieveResponse { get; init; }
        public List<StripeCheckoutCreateRequest> CreateRequests { get; } = [];
        public List<string> RetrieveRequests { get; } = [];

        public Task<StripeCheckoutSession> CreateAsync(StripeCheckoutCreateRequest request, CancellationToken cancellationToken)
        {
            CreateRequests.Add(request);
            return Task.FromResult(CreateResponse ?? throw new InvalidOperationException("No create response configured."));
        }

        public Task<StripeCheckoutSession> RetrieveAsync(string providerSessionReference, CancellationToken cancellationToken)
        {
            RetrieveRequests.Add(providerSessionReference);
            return Task.FromResult(RetrieveResponse ?? throw new InvalidOperationException("No retrieve response configured."));
        }
    }

    private sealed class FakeVerifier : IStripeWebhookVerifier
    {
        public StripeWebhookEvent? Event { get; init; }
        public int VerificationAttempts { get; private set; }

        public bool TryVerify(ReadOnlyMemory<byte> rawBody, string signature, out StripeWebhookEvent stripeEvent)
        {
            VerificationAttempts++;
            stripeEvent = Event!;
            return Event is not null;
        }
    }
}
