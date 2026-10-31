using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Payments.Persistence;
using LgymApi.Payments.Providers.Stripe;
using LgymApi.Payments.Workflows;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LgymApi.Payments.UnitTests.Stripe;

[TestFixture]
public sealed class PaymentsStripeIdempotencyRecoveryTests
{
    [Test]
    public async Task CreateCheckout_WhenLocalSessionSaveFailsAfterProviderSuccess_RetryUsesThePersistedCheckoutAndSameProviderIdempotencyKey()
    {
        var accountId = Id<User>.New();
        PaymentCheckout? persistedCheckout = null;
        var gateway = new RecordingGateway
        {
            CreateResponse = new StripeCheckoutSession("cs_test_retry", "https://checkout.stripe.test/retry", false, "unpaid")
        };
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        checkoutRepository.FindByAccountAndClientIdempotencyKeyAsync(accountId, "client-key", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(persistedCheckout));
        checkoutRepository.AddAsync(Arg.Do<PaymentCheckout>(checkout => persistedCheckout = checkout), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var unitOfWork = Substitute.For<IPaymentsWorkflowUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1), Task.FromException<int>(new DbUpdateException("local session save failed")), Task.FromResult(1));

        var firstAttempt = CreateService(gateway, checkoutRepository, unitOfWork);
        await FluentActions.Awaiting(() => firstAttempt.CreateCheckoutAsync(accountId, "client-key"))
            .Should().ThrowAsync<DbUpdateException>();
        persistedCheckout!.ProviderSessionReference = string.Empty;

        var retry = CreateService(gateway, checkoutRepository, unitOfWork);
        var result = await retry.CreateCheckoutAsync(accountId, "client-key");

        result.ProviderSessionReference.Should().Be("cs_test_retry");
        result.CheckoutUrl.Should().Be("https://checkout.stripe.test/retry");
        gateway.CreateRequests.Should().HaveCount(2);
        gateway.CreateRequests.Select(request => request.IdempotencyKey).Should().OnlyContain(key => key == gateway.CreateRequests[0].IdempotencyKey);
        await checkoutRepository.Received(1).AddAsync(Arg.Any<PaymentCheckout>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateCheckout_WhenPersistedSessionExists_ReusesTheProviderCheckoutWithoutCreatingAnotherSession()
    {
        var accountId = Id<User>.New();
        var checkout = new PaymentCheckout
        {
            Id = Id<PaymentCheckout>.New(),
            AccountId = accountId,
            ClientIdempotencyKey = "client-key",
            ProviderSessionReference = "cs_test_existing",
            Status = PaymentCheckoutStatus.Created
        };
        var gateway = new RecordingGateway
        {
            RetrieveResponse = new StripeCheckoutSession("cs_test_existing", "https://checkout.stripe.test/existing", false, "unpaid")
        };
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        checkoutRepository.FindByAccountAndClientIdempotencyKeyAsync(accountId, "client-key", Arg.Any<CancellationToken>())
            .Returns(checkout);

        var result = await CreateService(gateway, checkoutRepository, Substitute.For<IPaymentsWorkflowUnitOfWork>())
            .CreateCheckoutAsync(accountId, "client-key");

        result.CheckoutId.Should().Be(checkout.Id);
        result.ProviderSessionReference.Should().Be("cs_test_existing");
        result.CheckoutUrl.Should().Be("https://checkout.stripe.test/existing");
        gateway.CreateRequests.Should().BeEmpty();
        gateway.RetrieveRequests.Should().ContainSingle().Which.Should().Be("cs_test_existing");
    }

    [Test]
    public async Task HandleWebhook_WhenDuplicateDeliveriesRace_RecoversTheUniqueReceiptWinner()
    {
        var checkout = new PaymentCheckout
        {
            Id = Id<PaymentCheckout>.New(),
            AccountId = Id<User>.New(),
            ProviderSessionReference = "cs_test_duplicate",
            Status = PaymentCheckoutStatus.Created
        };
        var gateway = new SynchronizedRetrieveGateway(new StripeCheckoutSession(
            "cs_test_duplicate",
            "https://checkout.stripe.test/duplicate",
            false,
            "paid",
            new Dictionary<string, string> { ["lgym_payment_checkout_id"] = checkout.Id.ToString() },
            "price_test_configured",
            1));
        var checkoutRepository = Substitute.For<IPaymentCheckoutRepository>();
        checkoutRepository.FindForUpdateByProviderSessionReferenceAsync("cs_test_duplicate", Arg.Any<CancellationToken>()).Returns(checkout);
        var receiptRepository = Substitute.For<IPaymentEventReceiptRepository>();
        var receiptLookups = 0;
        receiptRepository.FindByProviderEventIdAsync("evt_duplicate", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PaymentEventReceipt?>(Interlocked.Increment(ref receiptLookups) <= 2 ? null : new PaymentEventReceipt()));
        receiptRepository.AddAsync(Arg.Any<PaymentEventReceipt>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var saveCount = 0;
        var firstSaveCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unitOfWork = Substitute.For<IPaymentsWorkflowUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref saveCount) == 1)
            {
                firstSaveCompleted.SetResult();
                return Task.FromResult(1);
            }

            firstSaveCompleted.Task.GetAwaiter().GetResult();
            return Task.FromException<int>(new DbUpdateException("duplicate receipt"));
        });

        var first = CreateService(gateway, checkoutRepository, unitOfWork, receiptRepository);
        var second = CreateService(gateway, checkoutRepository, unitOfWork, receiptRepository);
        var results = await Task.WhenAll(
            first.HandleWebhookAsync("{}"u8.ToArray(), "signature"),
            second.HandleWebhookAsync("{}"u8.ToArray(), "signature"));

        results.Should().BeEquivalentTo([StripeWebhookHandlingResult.Processed, StripeWebhookHandlingResult.Ignored]);
        checkout.Status.Should().Be(PaymentCheckoutStatus.Paid);
        await receiptRepository.Received(2).AddAsync(Arg.Any<PaymentEventReceipt>(), Arg.Any<CancellationToken>());
    }

    private static StripePaymentsService CreateService(
        IStripeCheckoutGateway gateway,
        IPaymentCheckoutRepository checkoutRepository,
        IPaymentsWorkflowUnitOfWork unitOfWork,
        IPaymentEventReceiptRepository? receiptRepository = null)
        => new(
            new StripePaymentOptions
            {
                Enabled = true,
                PriceId = "price_test_configured",
                SuccessUrl = "https://app.example.test/payments/success",
                CancelUrl = "https://app.example.test/payments/cancel",
                ApiKey = "sk_test_configured",
                WebhookSigningSecret = "whsec_configured"
            },
            gateway,
            new VerifiedWebhook("evt_duplicate", "cs_test_duplicate"),
            checkoutRepository,
            receiptRepository ?? Substitute.For<IPaymentEventReceiptRepository>(),
            unitOfWork);

    private sealed class RecordingGateway : IStripeCheckoutGateway
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

    private sealed class SynchronizedRetrieveGateway(StripeCheckoutSession session) : IStripeCheckoutGateway
    {
        private readonly TaskCompletionSource _bothRetrieves = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _retrieveCount;

        public Task<StripeCheckoutSession> CreateAsync(StripeCheckoutCreateRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public async Task<StripeCheckoutSession> RetrieveAsync(string providerSessionReference, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _retrieveCount) == 2)
            {
                _bothRetrieves.SetResult();
            }

            await _bothRetrieves.Task.WaitAsync(cancellationToken);
            return session;
        }
    }

    private sealed class VerifiedWebhook(string eventId, string sessionReference) : IStripeWebhookVerifier
    {
        public bool TryVerify(ReadOnlyMemory<byte> rawBody, string signature, out StripeWebhookEvent stripeEvent)
        {
            stripeEvent = new StripeWebhookEvent(eventId, "checkout.session.completed", false, sessionReference);
            return true;
        }
    }
}
